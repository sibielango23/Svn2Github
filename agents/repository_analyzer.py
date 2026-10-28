#!/usr/bin/env python3
"""
Repository Analyzer Agent
Analyzes SVN repository structure and generates migration metadata.
"""

import json
import sys
import subprocess
import re
from typing import Dict, Any, List
from pathlib import Path


class RepositoryAnalyzer:
    """Analyzes SVN repository structure."""

    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.svn_url = config['svn']['url']
        self.workspace = Path(config['workspace'])

    def analyze(self) -> Dict[str, Any]:
        """Perform comprehensive repository analysis."""
        results = {
            'structure': self._detect_structure(),
            'branches': self._list_branches(),
            'tags': self._list_tags(),
            'revision_range': self._get_revision_range(),
            'size_estimate': self._estimate_size(),
            'authors': self._extract_authors()
        }
        return results

    def _detect_structure(self) -> str:
        """Detect if repository follows standard SVN layout."""
        try:
            # Check for trunk/branches/tags
            result = subprocess.run(
                ['svn', 'list', self.svn_url],
                capture_output=True,
                text=True,
                check=True
            )
            contents = result.stdout.lower()

            if 'trunk/' in contents and 'branches/' in contents and 'tags/' in contents:
                return 'standard'
            else:
                return 'non-standard'

        except subprocess.CalledProcessError:
            return 'unknown'

    def _list_branches(self) -> List[str]:
        """List all branches in the repository."""
        try:
            branches_url = f"{self.svn_url}/branches"
            result = subprocess.run(
                ['svn', 'list', branches_url],
                capture_output=True,
                text=True,
                check=False
            )

            if result.returncode == 0:
                return [b.strip('/') for b in result.stdout.strip().split('\n') if b.strip()]
            return []

        except Exception:
            return []

    def _list_tags(self) -> List[str]:
        """List all tags in the repository."""
        try:
            tags_url = f"{self.svn_url}/tags"
            result = subprocess.run(
                ['svn', 'list', tags_url],
                capture_output=True,
                text=True,
                check=False
            )

            if result.returncode == 0:
                return [t.strip('/') for t in result.stdout.strip().split('\n') if t.strip()]
            return []

        except Exception:
            return []

    def _get_revision_range(self) -> Dict[str, int]:
        """Get first and last revision numbers."""
        try:
            result = subprocess.run(
                ['svn', 'log', '--quiet', '--limit', '1', self.svn_url],
                capture_output=True,
                text=True,
                check=True
            )

            # Extract latest revision
            match = re.search(r'r(\d+)', result.stdout)
            latest = int(match.group(1)) if match else None

            # Get first revision (expensive for large repos, so we estimate)
            return {
                'first': 1,  # Usually starts at 1
                'last': latest,
                'total': latest
            }

        except subprocess.CalledProcessError:
            return {'first': None, 'last': None, 'total': None}

    def _estimate_size(self) -> Dict[str, Any]:
        """Estimate repository size and complexity."""
        try:
            # Quick check of trunk size
            trunk_url = f"{self.svn_url}/trunk"
            result = subprocess.run(
                ['svn', 'list', '-R', trunk_url],
                capture_output=True,
                text=True,
                timeout=30,
                check=False
            )

            if result.returncode == 0:
                file_count = len(result.stdout.strip().split('\n'))
                return {
                    'trunk_files': file_count,
                    'estimated_complexity': 'high' if file_count > 10000 else 'medium' if file_count > 1000 else 'low'
                }

        except Exception:
            pass

        return {'trunk_files': None, 'estimated_complexity': 'unknown'}

    def _extract_authors(self) -> List[str]:
        """Extract unique author list from SVN history."""
        try:
            # Get last 1000 commits to extract authors (faster than full history)
            result = subprocess.run(
                ['svn', 'log', '--quiet', '--limit', '1000', self.svn_url],
                capture_output=True,
                text=True,
                check=True
            )

            # Extract usernames from log
            authors = set()
            for line in result.stdout.split('\n'):
                match = re.search(r'r\d+ \| (\S+) \|', line)
                if match:
                    authors.add(match.group(1))

            return sorted(list(authors))

        except subprocess.CalledProcessError:
            return []


def main():
    # Read configuration from stdin
    config = json.load(sys.stdin)

    analyzer = RepositoryAnalyzer(config)
    results = analyzer.analyze()

    # Save results to workspace
    workspace = Path(config['workspace'])
    output_file = workspace / 'analysis-results.json'
    with open(output_file, 'w') as f:
        json.dump(results, f, indent=2)

    # Output results
    results['output_file'] = str(output_file)
    print(json.dumps(results))


if __name__ == '__main__':
    main()
