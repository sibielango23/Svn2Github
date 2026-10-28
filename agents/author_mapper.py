#!/usr/bin/env python3
"""
Author Mapper Agent
Extracts SVN authors and creates Git author mapping file.
"""

import json
import sys
import subprocess
import re
from typing import Dict, List, Any
from pathlib import Path


class AuthorMapper:
    """Maps SVN usernames to Git author identities."""

    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.svn_url = config['svn']['url']
        self.workspace = Path(config['workspace'])
        self.default_domain = config.get('author_domain', 'example.com')

    def create_mapping(self) -> Dict[str, Any]:
        """Create author mapping file for git-svn."""
        authors = self._extract_all_authors()
        authors_file = self._generate_authors_file(authors)

        return {
            'authors': authors,
            'authors_count': len(authors),
            'file': str(authors_file)
        }

    def _extract_all_authors(self) -> List[str]:
        """Extract all unique authors from complete SVN history."""
        try:
            # For 15 years of history, this might take a while
            # We'll extract authors without full log content for speed
            result = subprocess.run(
                ['svn', 'log', '--quiet', self.svn_url],
                capture_output=True,
                text=True,
                check=True,
                timeout=600  # 10 minute timeout for large repos
            )

            authors = set()
            for line in result.stdout.split('\n'):
                match = re.search(r'r\d+ \| (\S+) \|', line)
                if match:
                    authors.add(match.group(1))

            return sorted(list(authors))

        except subprocess.CalledProcessError as e:
            print(f"Error extracting authors: {e}", file=sys.stderr)
            return []
        except subprocess.TimeoutExpired:
            print("Timeout while extracting authors, using sample", file=sys.stderr)
            # Fallback to limited extraction
            return self._extract_sample_authors()

    def _extract_sample_authors(self) -> List[str]:
        """Extract authors from recent history as fallback."""
        try:
            result = subprocess.run(
                ['svn', 'log', '--quiet', '--limit', '5000', self.svn_url],
                capture_output=True,
                text=True,
                check=True
            )

            authors = set()
            for line in result.stdout.split('\n'):
                match = re.search(r'r\d+ \| (\S+) \|', line)
                if match:
                    authors.add(match.group(1))

            return sorted(list(authors))

        except subprocess.CalledProcessError:
            return []

    def _generate_authors_file(self, authors: List[str]) -> Path:
        """Generate authors.txt file in git-svn format."""
        authors_file = self.workspace / 'authors.txt'

        with open(authors_file, 'w', encoding='utf-8') as f:
            for author in authors:
                # Format: svn_username = Full Name <email@example.com>
                # You can customize this mapping as needed
                full_name = self._guess_full_name(author)
                email = f"{author}@{self.default_domain}"
                f.write(f"{author} = {full_name} <{email}>\n")

        return authors_file

    def _guess_full_name(self, username: str) -> str:
        """Convert username to a readable full name."""
        # Simple heuristic: convert firstname.lastname to Firstname Lastname
        if '.' in username:
            parts = username.split('.')
            return ' '.join(p.capitalize() for p in parts)
        else:
            return username.capitalize()


def main():
    config = json.load(sys.stdin)

    mapper = AuthorMapper(config)
    results = mapper.create_mapping()

    print(json.dumps(results))


if __name__ == '__main__':
    main()
