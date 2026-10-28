#!/usr/bin/env python3
"""
Monitoring utility for tracking migration progress.
Usage: python monitor.py [--workspace <path>]
"""

import argparse
import json
import subprocess
import time
from pathlib import Path
from datetime import datetime


class MigrationMonitor:
    """Monitors ongoing migration progress."""

    def __init__(self, workspace: str = "./migration-workspace"):
        self.workspace = Path(workspace)
        self.repo_path = self.workspace / "migrated-repo"

    def check_status(self):
        """Check current migration status."""
        print("\n" + "="*60)
        print("SVN to GitHub Migration Status")
        print("="*60)
        print(f"Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")

        # Check if workspace exists
        if not self.workspace.exists():
            print("❌ Migration workspace not found")
            print(f"   Expected: {self.workspace}")
            return

        print(f"✓ Workspace: {self.workspace}")

        # Check for analysis results
        analysis_file = self.workspace / "analysis-results.json"
        if analysis_file.exists():
            print("✓ Repository analysis completed")
            with open(analysis_file) as f:
                data = json.load(f)
                if 'revision_range' in data:
                    rev_range = data['revision_range']
                    print(f"  Total revisions: {rev_range.get('total', 'unknown')}")
        else:
            print("⏳ Repository analysis not started")

        # Check for authors file
        authors_file = self.workspace / "authors.txt"
        if authors_file.exists():
            with open(authors_file) as f:
                author_count = len(f.readlines())
            print(f"✓ Author mapping completed ({author_count} authors)")
        else:
            print("⏳ Author mapping not started")

        # Check migration progress
        if not self.repo_path.exists():
            print("⏳ Migration not started")
            return

        print("✓ Git repository created")

        # Get repository statistics
        stats = self._get_repo_stats()

        if stats:
            print(f"\nRepository Statistics:")
            print(f"  Commits: {stats.get('commits', 0)}")
            print(f"  Branches: {stats.get('branches', 0)}")
            print(f"  Tags: {stats.get('tags', 0)}")
            print(f"  Size: {stats.get('size', 'unknown')}")

        # Check for remote
        if self._has_remote():
            print("\n✓ GitHub remote configured")

            # Check push status
            push_status = self._check_push_status()
            if push_status['pushed']:
                print("✓ Pushed to GitHub")
                print(f"  Remote commits: {push_status.get('remote_commits', 'unknown')}")
            else:
                print("⏳ Not yet pushed to GitHub")
        else:
            print("\n⏳ GitHub remote not configured")

        # Check for migration report
        report_file = self.workspace / "migration-report.json"
        if report_file.exists():
            print("\n✅ Migration completed!")
            print(f"   Report: {report_file}")
        else:
            print("\n⏳ Migration in progress...")

        print("="*60 + "\n")

    def _get_repo_stats(self):
        """Get repository statistics."""
        try:
            # Count commits
            result = subprocess.run(
                ['git', 'rev-list', '--all', '--count'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            commits = int(result.stdout.strip())

            # Count branches
            result = subprocess.run(
                ['git', 'branch', '-a'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            branches = len([b for b in result.stdout.split('\n') if b.strip()])

            # Count tags
            result = subprocess.run(
                ['git', 'tag'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            tags = len([t for t in result.stdout.split('\n') if t.strip()])

            # Get size
            result = subprocess.run(
                ['git', 'count-objects', '-vH'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            size_line = [line for line in result.stdout.split('\n')
                        if 'size-pack' in line]
            size = size_line[0].split(':')[1].strip() if size_line else 'unknown'

            return {
                'commits': commits,
                'branches': branches,
                'tags': tags,
                'size': size
            }

        except Exception:
            return None

    def _has_remote(self):
        """Check if remote is configured."""
        try:
            result = subprocess.run(
                ['git', 'remote', 'get-url', 'origin'],
                cwd=str(self.repo_path),
                capture_output=True,
                check=True
            )
            return result.returncode == 0
        except subprocess.CalledProcessError:
            return False

    def _check_push_status(self):
        """Check if repository has been pushed."""
        try:
            # Count remote commits
            result = subprocess.run(
                ['git', 'rev-list', '--remotes', '--count'],
                cwd=str(self.repo_path),
                capture_output=True,
                text=True,
                check=True
            )
            remote_commits = int(result.stdout.strip())

            return {
                'pushed': remote_commits > 0,
                'remote_commits': remote_commits
            }

        except Exception:
            return {'pushed': False}

    def watch(self, interval: int = 60):
        """Watch migration progress with periodic updates."""
        print("Watching migration progress (Ctrl+C to stop)...")
        try:
            while True:
                self.check_status()
                time.sleep(interval)
        except KeyboardInterrupt:
            print("\nStopped watching.")


def main():
    parser = argparse.ArgumentParser(description='Monitor SVN to GitHub migration')
    parser.add_argument('--workspace', default='./migration-workspace',
                       help='Migration workspace directory')
    parser.add_argument('--watch', action='store_true',
                       help='Continuously watch progress')
    parser.add_argument('--interval', type=int, default=60,
                       help='Update interval in seconds (for watch mode)')

    args = parser.parse_args()

    monitor = MigrationMonitor(args.workspace)

    if args.watch:
        monitor.watch(args.interval)
    else:
        monitor.check_status()


if __name__ == '__main__':
    main()
