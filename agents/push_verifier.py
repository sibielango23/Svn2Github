#!/usr/bin/env python3
"""
Push & Verification Agent
Pushes repository to GitHub and verifies migration integrity.
"""

import json
import sys
import subprocess
import time
from typing import Dict, Any
from pathlib import Path


class PushVerifier:
    """Pushes repository and verifies migration."""

    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.workspace = Path(config['workspace'])
        self.repo_path = self.workspace / 'migrated-repo'
        self.remote_url = config.get('remote_url')

    def push_and_verify(self) -> Dict[str, Any]:
        """Push to GitHub and verify."""
        # Add remote
        remote_result = self._add_remote()
        if not remote_result['success']:
            return remote_result

        # Push all branches
        push_result = self._push_all()
        if not push_result['success']:
            return push_result

        # Verify integrity
        verification = self._verify_migration()

        return {
            'success': True,
            'pushed': push_result['pushed'],
            'verification': verification
        }

    def _add_remote(self) -> Dict[str, Any]:
        """Add GitHub remote to repository."""
        try:
            # Check if remote already exists
            result = subprocess.run(
                ['git', 'remote', 'get-url', 'origin'],
                cwd=str(self.repo_path),
                capture_output=True,
                check=False
            )

            if result.returncode == 0:
                # Remote exists, update URL
                subprocess.run(
                    ['git', 'remote', 'set-url', 'origin', self.remote_url],
                    cwd=str(self.repo_path),
                    capture_output=True,
                    check=True
                )
            else:
                # Add new remote
                subprocess.run(
                    ['git', 'remote', 'add', 'origin', self.remote_url],
                    cwd=str(self.repo_path),
                    capture_output=True,
                    check=True
                )

            return {'success': True}

        except subprocess.CalledProcessError as e:
            return {
                'success': False,
                'error': f"Failed to add remote: {e.stderr}"
            }

    def _push_all(self) -> Dict[str, Any]:
        """Push all branches and tags to GitHub."""
        pushed = {
            'branches': [],
            'tags': []
        }

        try:
            # Push all branches
            result = subprocess.run(
                ['git', 'push', 'origin', '--all'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            print(result.stdout, file=sys.stderr)

            # Get list of pushed branches
            result = subprocess.run(
                ['git', 'branch', '-r'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            pushed['branches'] = [b.strip() for b in result.stdout.split('\n') if b.strip()]

            # Push all tags
            result = subprocess.run(
                ['git', 'push', 'origin', '--tags'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            print(result.stdout, file=sys.stderr)

            # Get list of pushed tags
            result = subprocess.run(
                ['git', 'tag'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            pushed['tags'] = [t.strip() for t in result.stdout.split('\n') if t.strip()]

            return {
                'success': True,
                'pushed': pushed
            }

        except subprocess.CalledProcessError as e:
            return {
                'success': False,
                'error': f"Push failed: {e.stderr}",
                'partial_push': pushed
            }

    def _verify_migration(self) -> Dict[str, Any]:
        """Verify migration integrity."""
        verification = {}

        try:
            # Count local commits
            result = subprocess.run(
                ['git', 'rev-list', '--all', '--count'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            local_commits = int(result.stdout.strip())

            # Count remote commits
            result = subprocess.run(
                ['git', 'rev-list', '--remotes', '--count'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            remote_commits = int(result.stdout.strip())

            verification['local_commits'] = local_commits
            verification['remote_commits'] = remote_commits
            verification['commits_match'] = (local_commits == remote_commits)

            # Check for any unpushed commits
            result = subprocess.run(
                ['git', 'log', '--branches', '--not', '--remotes', '--oneline'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            unpushed = result.stdout.strip()
            verification['unpushed_commits'] = len(unpushed.split('\n')) if unpushed else 0

            # Overall status
            verification['status'] = 'success' if verification['commits_match'] and verification['unpushed_commits'] == 0 else 'warning'

            return verification

        except subprocess.CalledProcessError as e:
            return {
                'status': 'error',
                'error': str(e)
            }


def main():
    config = json.load(sys.stdin)

    verifier = PushVerifier(config)
    results = verifier.push_and_verify()

    print(json.dumps(results))


if __name__ == '__main__':
    main()
