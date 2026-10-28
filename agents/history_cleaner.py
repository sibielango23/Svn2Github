#!/usr/bin/env python3
"""
History Cleaner Agent
Cleans up unwanted artifacts from repository history using git-filter-repo.
"""

import json
import sys
import subprocess
from typing import Dict, Any, List
from pathlib import Path
import shutil


class HistoryCleaner:
    """Cleans repository history of unwanted files."""

    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.workspace = Path(config['workspace'])
        self.repo_path = self.workspace / 'migrated-repo'
        self.exclude_patterns = config.get('options', {}).get('exclude_paths', [])
        self.max_file_size_mb = config.get('options', {}).get('max_file_size_mb', 100)

    def clean(self) -> Dict[str, Any]:
        """Clean repository history."""
        # Check if git-filter-repo is available
        if not self._check_filter_repo():
            return {
                'success': False,
                'error': 'git-filter-repo not found',
                'message': 'Install with: pip install git-filter-repo'
            }

        stats_before = self._get_repo_stats()

        # Build filter-repo command
        filters = []

        # Add path-based exclusions
        for pattern in self.exclude_patterns:
            filters.extend(['--path-glob', pattern, '--invert-paths'])

        # Add size-based filter
        if self.max_file_size_mb:
            max_size_bytes = self.max_file_size_mb * 1024 * 1024
            filters.extend(['--strip-blobs-bigger-than', f'{max_size_bytes}'])

        if not filters:
            return {
                'success': True,
                'message': 'No cleaning filters specified',
                'stats_before': stats_before
            }

        try:
            # Run git-filter-repo
            cmd = ['git', 'filter-repo'] + filters + ['--force']

            result = subprocess.run(
                cmd,
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )

            stats_after = self._get_repo_stats()

            return {
                'success': True,
                'stats_before': stats_before,
                'stats_after': stats_after,
                'size_reduction_mb': (stats_before['size_mb'] - stats_after['size_mb'])
            }

        except subprocess.CalledProcessError as e:
            return {
                'success': False,
                'error': e.stderr
            }

    def _check_filter_repo(self) -> bool:
        """Check if git-filter-repo is installed."""
        try:
            subprocess.run(
                ['git', 'filter-repo', '--version'],
                capture_output=True,
                check=True
            )
            return True
        except (subprocess.CalledProcessError, FileNotFoundError):
            return False

    def _get_repo_stats(self) -> Dict[str, Any]:
        """Get repository statistics."""
        try:
            # Get repository size
            result = subprocess.run(
                ['git', 'count-objects', '-vH'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )

            stats = {}
            for line in result.stdout.split('\n'):
                if ':' in line:
                    key, value = line.split(':', 1)
                    stats[key.strip()] = value.strip()

            # Extract size in MB
            size_str = stats.get('size-pack', '0 MiB')
            size_mb = float(size_str.split()[0])

            return {
                'size_mb': size_mb,
                'raw_stats': stats
            }

        except Exception as e:
            return {'error': str(e)}


def main():
    config = json.load(sys.stdin)

    cleaner = HistoryCleaner(config)
    results = cleaner.clean()

    print(json.dumps(results))


if __name__ == '__main__':
    main()
