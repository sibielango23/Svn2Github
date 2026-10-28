#!/usr/bin/env python3
"""
Main orchestrator for SVN to GitHub migration using multi-agent system.
Coordinates all migration agents and manages the end-to-end workflow.
"""

import argparse
import json
import logging
import sys
from pathlib import Path
from typing import Dict, Any
import subprocess
import time

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger('orchestrator')


class MigrationOrchestrator:
    """Coordinates all agents in the migration pipeline."""

    def __init__(self, config_path: str = None):
        self.config = self._load_config(config_path)
        self.workspace = Path(self.config.get('workspace', './migration-workspace'))
        self.workspace.mkdir(exist_ok=True)
        self.state = {}

    def _load_config(self, config_path: str) -> Dict[str, Any]:
        """Load migration configuration."""
        if config_path and Path(config_path).exists():
            with open(config_path) as f:
                return json.load(f)
        return {}

    def run_agent(self, agent_name: str, **kwargs) -> Dict[str, Any]:
        """Execute a specific agent and return its results."""
        logger.info(f"Starting agent: {agent_name}")
        start_time = time.time()

        agent_module = f"agents.{agent_name}"
        agent_script = Path(__file__).parent / 'agents' / f'{agent_name}.py'

        if not agent_script.exists():
            raise FileNotFoundError(f"Agent script not found: {agent_script}")

        # Build agent command
        cmd = [sys.executable, str(agent_script)]

        # Pass configuration as JSON via stdin
        agent_config = {
            **self.config,
            **kwargs,
            'workspace': str(self.workspace)
        }

        try:
            result = subprocess.run(
                cmd,
                input=json.dumps(agent_config),
                capture_output=True,
                text=True,
                check=True
            )

            output = json.loads(result.stdout) if result.stdout else {}
            duration = time.time() - start_time

            logger.info(f"Agent {agent_name} completed in {duration:.2f}s")
            return {
                'success': True,
                'data': output,
                'duration': duration
            }

        except subprocess.CalledProcessError as e:
            logger.error(f"Agent {agent_name} failed: {e.stderr}")
            return {
                'success': False,
                'error': e.stderr,
                'duration': time.time() - start_time
            }

    def execute_migration(self):
        """Run the complete migration pipeline."""
        logger.info("Starting SVN to GitHub migration")

        # Phase 1: Analysis
        logger.info("=== Phase 1: Repository Analysis ===")
        analysis_result = self.run_agent('repository_analyzer')
        if not analysis_result['success']:
            logger.error("Repository analysis failed")
            return False
        self.state['analysis'] = analysis_result['data']

        # Phase 2: Author Mapping
        logger.info("=== Phase 2: Author Mapping ===")
        author_result = self.run_agent('author_mapper')
        if not author_result['success']:
            logger.error("Author mapping failed")
            return False
        self.state['authors'] = author_result['data']

        # Phase 3: Migration
        logger.info("=== Phase 3: Git-SVN Migration ===")
        migration_result = self.run_agent('migration_executor',
                                         authors_file=self.state['authors'].get('file'))
        if not migration_result['success']:
            logger.error("Migration execution failed")
            return False
        self.state['migration'] = migration_result['data']

        # Phase 4: History Cleaning (optional)
        if self.config.get('options', {}).get('clean_history', False):
            logger.info("=== Phase 4: History Cleaning ===")
            clean_result = self.run_agent('history_cleaner')
            if not clean_result['success']:
                logger.warning("History cleaning failed, continuing anyway")
            else:
                self.state['cleaning'] = clean_result['data']

        # Phase 5: GitHub Setup
        logger.info("=== Phase 5: GitHub Repository Setup ===")
        setup_result = self.run_agent('github_setup')
        if not setup_result['success']:
            logger.error("GitHub setup failed")
            return False
        self.state['github'] = setup_result['data']

        # Phase 6: Push and Verify
        logger.info("=== Phase 6: Push & Verification ===")
        push_result = self.run_agent('push_verifier',
                                    remote_url=self.state['github'].get('clone_url'))
        if not push_result['success']:
            logger.error("Push and verification failed")
            return False
        self.state['verification'] = push_result['data']

        # Generate final report
        self._generate_report()

        logger.info("Migration completed successfully!")
        return True

    def _generate_report(self):
        """Generate migration report."""
        report_path = self.workspace / 'migration-report.json'
        with open(report_path, 'w') as f:
            json.dump(self.state, f, indent=2)
        logger.info(f"Migration report saved to: {report_path}")


def main():
    parser = argparse.ArgumentParser(description='SVN to GitHub Migration Orchestrator')
    parser.add_argument('--config', help='Path to configuration file')
    parser.add_argument('--svn-url', help='SVN repository URL')
    parser.add_argument('--github-org', help='GitHub organization')
    parser.add_argument('--github-repo', help='GitHub repository name')
    parser.add_argument('--workspace', help='Working directory for migration')

    args = parser.parse_args()

    # Build config from args
    config = {}
    if args.svn_url:
        config['svn'] = {'url': args.svn_url}
    if args.github_org and args.github_repo:
        config['github'] = {'org': args.github_org, 'repo': args.github_repo}
    if args.workspace:
        config['workspace'] = args.workspace

    # Save temp config if provided via CLI
    if config and not args.config:
        config_path = Path('./temp-migration-config.json')
        with open(config_path, 'w') as f:
            json.dump(config, f, indent=2)
        args.config = str(config_path)

    orchestrator = MigrationOrchestrator(args.config)
    success = orchestrator.execute_migration()
    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
