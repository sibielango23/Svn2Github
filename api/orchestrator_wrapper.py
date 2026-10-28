"""
Orchestrator wrapper that integrates with the API and database.
Wraps the existing orchestrator.py to work with REST API.
"""

import sys
import os
import json
import asyncio
from pathlib import Path
from datetime import datetime
import subprocess

# Add parent directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from api.database import Log, Statistic, Author, VerificationResult


class MigrationOrchestrator:
    """API wrapper for the migration orchestrator."""

    def __init__(self, migration_id: str, config: dict, db_session, websocket_manager):
        self.migration_id = migration_id
        self.config = config
        self.db = db_session
        self.ws_manager = websocket_manager
        self.workspace = Path(config['workspace'])
        self.workspace.mkdir(parents=True, exist_ok=True)

    async def execute(self):
        """Execute the migration pipeline."""
        phases = [
            ("Repository Analysis", self._run_analyzer),
            ("Author Mapping", self._run_author_mapper),
            ("Migration Execution", self._run_migration_executor),
        ]

        # Optional phases
        if self.config.get('options', {}).get('clean_history', False):
            phases.append(("History Cleaning", self._run_history_cleaner))

        phases.extend([
            ("GitHub Setup", self._run_github_setup),
            ("Push & Verification", self._run_push_verifier)
        ])

        for i, (phase_name, phase_func) in enumerate(phases, 1):
            await self._log(f"Starting phase {i}/{len(phases)}: {phase_name}", "INFO", phase_name.lower())
            await self._update_progress(phase_name, (i - 1) / len(phases) * 100)

            try:
                await phase_func()
            except Exception as e:
                await self._log(f"Phase failed: {str(e)}", "ERROR", phase_name.lower())
                raise

            await self._log(f"Completed phase: {phase_name}", "INFO", phase_name.lower())

        await self._update_progress("Completed", 100.0)

    async def _run_analyzer(self):
        """Run repository analyzer agent."""
        result = await self._run_agent("repository_analyzer")

        if result.get('success'):
            data = result.get('data', {})

            # Store statistics
            if 'revision_range' in data:
                rev_range = data['revision_range']
                self._store_statistic("total_revisions", str(rev_range.get('total', 0)), "analysis")

            if 'branches' in data:
                self._store_statistic("branches_count", str(len(data['branches'])), "analysis")

            if 'tags' in data:
                self._store_statistic("tags_count", str(len(data['tags'])), "analysis")

            # Update migration total revisions
            from api.database import Migration
            migration = self.db.query(Migration).filter(Migration.id == self.migration_id).first()
            if migration and 'revision_range' in data:
                migration.total_revisions = data['revision_range'].get('total')
                self.db.commit()

    async def _run_author_mapper(self):
        """Run author mapper agent."""
        result = await self._run_agent("author_mapper")

        if result.get('success'):
            data = result.get('data', {})
            authors_list = data.get('authors', [])

            # Store authors in database
            for svn_username in authors_list:
                author = Author(
                    migration_id=self.migration_id,
                    svn_username=svn_username,
                    git_name=self._guess_full_name(svn_username),
                    git_email=f"{svn_username}@{self.config['options']['author_domain']}",
                    verified=False
                )
                self.db.add(author)

            self.db.commit()

            self._store_statistic("authors_count", str(len(authors_list)), "author_mapping")

    async def _run_migration_executor(self):
        """Run migration executor agent."""
        # This is the longest-running phase, so we'll monitor it more closely
        result = await self._run_agent("migration_executor")

        if result.get('success'):
            data = result.get('data', {})
            stats = data.get('statistics', {})

            self._store_statistic("commits", str(stats.get('commits', 0)), "migration")
            self._store_statistic("branches", str(stats.get('branches', 0)), "migration")
            self._store_statistic("tags", str(stats.get('tags', 0)), "migration")

    async def _run_history_cleaner(self):
        """Run history cleaner agent (optional)."""
        result = await self._run_agent("history_cleaner")

        if result.get('success'):
            data = result.get('data', {})
            if 'size_reduction_mb' in data:
                self._store_statistic("size_reduction_mb", str(data['size_reduction_mb']), "cleaning")

    async def _run_github_setup(self):
        """Run GitHub setup agent."""
        result = await self._run_agent("github_setup")

        if result.get('success'):
            data = result.get('data', {})
            self._store_statistic("github_url", data.get('web_url', ''), "github_setup")

    async def _run_push_verifier(self):
        """Run push and verification agent."""
        result = await self._run_agent("push_verifier")

        if result.get('success'):
            data = result.get('data', {})
            verification = data.get('verification', {})

            # Store verification results
            checks = [
                ("commits_match", verification.get('commits_match', False)),
                ("no_unpushed", verification.get('unpushed_commits', 0) == 0)
            ]

            for check_name, passed in checks:
                result_entry = VerificationResult(
                    migration_id=self.migration_id,
                    check_name=check_name,
                    status="passed" if passed else "failed",
                    message=f"Verification check: {check_name}"
                )
                self.db.add(result_entry)

            self.db.commit()

    async def _run_agent(self, agent_name: str) -> dict:
        """Run a specific agent and return its results."""
        agent_script = Path(__file__).parent.parent / 'agents' / f'{agent_name}.py'

        if not agent_script.exists():
            raise FileNotFoundError(f"Agent script not found: {agent_script}")

        # Build agent command
        cmd = [sys.executable, str(agent_script)]

        try:
            # Run agent
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )

            # Send config as JSON
            config_json = json.dumps(self.config).encode()
            stdout, stderr = await process.communicate(input=config_json)

            # Log stderr output
            if stderr:
                await self._log(stderr.decode(), "DEBUG", agent_name)

            # Parse stdout as JSON result
            if stdout:
                return json.loads(stdout.decode())
            else:
                return {"success": False, "error": "No output from agent"}

        except Exception as e:
            await self._log(f"Agent {agent_name} failed: {str(e)}", "ERROR", agent_name)
            return {"success": False, "error": str(e)}

    async def _log(self, message: str, level: str = "INFO", agent: str = None):
        """Log a message to database and WebSocket."""
        # Store in database
        log = Log(
            migration_id=self.migration_id,
            level=level,
            agent=agent,
            message=message
        )
        self.db.add(log)
        self.db.commit()

        # Broadcast via WebSocket
        await self.ws_manager.broadcast(self.migration_id, {
            "timestamp": datetime.utcnow().isoformat(),
            "level": level,
            "agent": agent,
            "message": message
        })

    async def _update_progress(self, phase: str, percent: float):
        """Update migration progress."""
        from api.database import Migration

        migration = self.db.query(Migration).filter(Migration.id == self.migration_id).first()
        if migration:
            migration.current_phase = phase
            migration.progress_percent = percent
            self.db.commit()

        # Broadcast status update
        await self.ws_manager.broadcast(self.migration_id, {
            "type": "status_update",
            "phase": phase,
            "progress": percent
        })

    def _store_statistic(self, metric_name: str, metric_value: str, phase: str = None):
        """Store a statistic in the database."""
        stat = Statistic(
            migration_id=self.migration_id,
            metric_name=metric_name,
            metric_value=metric_value,
            phase=phase
        )
        self.db.add(stat)
        self.db.commit()

    def _guess_full_name(self, username: str) -> str:
        """Convert username to readable full name."""
        if '.' in username:
            parts = username.split('.')
            return ' '.join(p.capitalize() for p in parts)
        return username.capitalize()
