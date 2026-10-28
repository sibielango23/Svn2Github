#!/usr/bin/env python3
"""
GitHub Setup Agent
Creates and configures GitHub repository using GitHub CLI or API.
"""

import json
import sys
import subprocess
from typing import Dict, Any
from pathlib import Path


class GitHubSetup:
    """Creates and configures GitHub repository."""

    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.org = config['github']['org']
        self.repo = config['github']['repo']
        self.private = config['github'].get('private', True)
        self.workspace = Path(config['workspace'])

    def setup(self) -> Dict[str, Any]:
        """Create and configure GitHub repository."""
        # Check if gh CLI is available
        if not self._check_gh_cli():
            return {
                'success': False,
                'error': 'GitHub CLI not found',
                'message': 'Install from: https://cli.github.com/'
            }

        # Create repository
        repo_result = self._create_repository()
        if not repo_result['success']:
            return repo_result

        # Configure repository settings
        self._configure_repository()

        return {
            'success': True,
            'org': self.org,
            'repo': self.repo,
            'clone_url': f"https://github.com/{self.org}/{self.repo}.git",
            'ssh_url': f"git@github.com:{self.org}/{self.repo}.git",
            'web_url': f"https://github.com/{self.org}/{self.repo}"
        }

    def _check_gh_cli(self) -> bool:
        """Check if GitHub CLI is installed and authenticated."""
        try:
            result = subprocess.run(
                ['gh', 'auth', 'status'],
                capture_output=True,
                check=True
            )
            return True
        except (subprocess.CalledProcessError, FileNotFoundError):
            return False

    def _create_repository(self) -> Dict[str, Any]:
        """Create GitHub repository."""
        try:
            cmd = [
                'gh', 'repo', 'create',
                f"{self.org}/{self.repo}",
                '--description', 'Migrated from SVN',
            ]

            if self.private:
                cmd.append('--private')
            else:
                cmd.append('--public')

            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                check=True
            )

            return {
                'success': True,
                'message': 'Repository created successfully'
            }

        except subprocess.CalledProcessError as e:
            # Check if repo already exists
            if 'already exists' in e.stderr.lower():
                return {
                    'success': True,
                    'message': 'Repository already exists, will use existing'
                }
            return {
                'success': False,
                'error': e.stderr
            }

    def _configure_repository(self):
        """Configure repository settings."""
        try:
            # Enable issues
            subprocess.run(
                ['gh', 'repo', 'edit', f"{self.org}/{self.repo}",
                 '--enable-issues'],
                capture_output=True,
                check=False
            )

            # Enable wiki
            subprocess.run(
                ['gh', 'repo', 'edit', f"{self.org}/{self.repo}",
                 '--enable-wiki'],
                capture_output=True,
                check=False
            )

            # Add topics
            subprocess.run(
                ['gh', 'repo', 'edit', f"{self.org}/{self.repo}",
                 '--add-topic', 'svn-migration'],
                capture_output=True,
                check=False
            )

        except Exception as e:
            print(f"Warning: Some configuration failed: {e}", file=sys.stderr)


def main():
    config = json.load(sys.stdin)

    setup = GitHubSetup(config)
    results = setup.setup()

    print(json.dumps(results))


if __name__ == '__main__':
    main()
