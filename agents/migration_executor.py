#!/usr/bin/env python3
"""
Migration Executor Agent
Performs the actual git-svn clone operation with progress monitoring.
"""

import json
import sys
import subprocess
import time
from typing import Dict, Any
from pathlib import Path


class MigrationExecutor:
    """Executes git-svn migration."""

    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.svn_url = config['svn']['url']
        self.workspace = Path(config['workspace'])
        self.repo_path = self.workspace / 'migrated-repo'
        self.authors_file = config.get('authors_file')

    def execute(self) -> Dict[str, Any]:
        """Execute the git-svn clone operation."""
        start_time = time.time()

        # Prepare clone command
        cmd = self._build_clone_command()

        print(f"Starting git-svn clone...", file=sys.stderr)
        print(f"Command: {' '.join(cmd)}", file=sys.stderr)

        try:
            # Run git-svn clone (this will take a while for 15 years of history)
            process = subprocess.Popen(
                cmd,
                cwd=str(self.workspace),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1
            )

            # Stream output for progress monitoring
            for line in process.stdout:
                print(line.strip(), file=sys.stderr)

            process.wait()

            if process.returncode != 0:
                return {
                    'success': False,
                    'error': 'git-svn clone failed',
                    'duration': time.time() - start_time
                }

            # Verify the migration
            stats = self._collect_statistics()

            return {
                'success': True,
                'repo_path': str(self.repo_path),
                'duration': time.time() - start_time,
                'statistics': stats
            }

        except Exception as e:
            return {
                'success': False,
                'error': str(e),
                'duration': time.time() - start_time
            }

    def _build_clone_command(self) -> list:
        """Build the git-svn clone command."""
        cmd = ['git', 'svn', 'clone']

        # Add SVN structure flags
        structure = self.config.get('svn', {}).get('structure', 'standard')
        if structure == 'standard':
            cmd.append('--stdlayout')
        else:
            # For non-standard layouts, specify trunk/branches/tags manually
            trunk = self.config.get('svn', {}).get('trunk', 'trunk')
            branches = self.config.get('svn', {}).get('branches', 'branches')
            tags = self.config.get('svn', {}).get('tags', 'tags')
            cmd.extend(['--trunk', trunk, '--branches', branches, '--tags', tags])

        # Add authors file if provided
        if self.authors_file:
            cmd.extend(['--authors-file', self.authors_file])

        # Preserve commit timestamps
        cmd.append('--no-metadata')

        # Add SVN URL and target directory
        cmd.extend([self.svn_url, str(self.repo_path)])

        return cmd

    def _collect_statistics(self) -> Dict[str, Any]:
        """Collect statistics about the migrated repository."""
        try:
            # Count commits
            result = subprocess.run(
                ['git', 'rev-list', '--all', '--count'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            commit_count = int(result.stdout.strip())

            # Count branches
            result = subprocess.run(
                ['git', 'branch', '-a'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            branch_count = len([b for b in result.stdout.strip().split('\n') if b.strip()])

            # Count tags
            result = subprocess.run(
                ['git', 'tag'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            tag_count = len([t for t in result.stdout.strip().split('\n') if t.strip()])

            return {
                'commits': commit_count,
                'branches': branch_count,
                'tags': tag_count
            }

        except Exception as e:
            return {'error': str(e)}


def main():
    config = json.load(sys.stdin)

    executor = MigrationExecutor(config)
    results = executor.execute()

    print(json.dumps(results))


if __name__ == '__main__':
    main()
