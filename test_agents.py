#!/usr/bin/env python3
"""
Test script for individual SVN to GitHub migration agents
"""

import json
import subprocess
import sys
from pathlib import Path

def test_agent(agent_name: str, config: dict) -> dict:
    """Run an agent with test config and return output"""
    print(f"\n{'='*60}")
    print(f"Testing: {agent_name}")
    print(f"{'='*60}")

    agent_path = Path(__file__).parent / "agents" / f"{agent_name}.py"

    if not agent_path.exists():
        print(f"❌ Agent file not found: {agent_path}")
        return {"status": "error", "message": "Agent not found"}

    print(f"Config: {json.dumps(config, indent=2)}")

    try:
        result = subprocess.run(
            [sys.executable, str(agent_path)],
            input=json.dumps(config),
            capture_output=True,
            text=True,
            timeout=30
        )

        if result.returncode != 0:
            print(f"❌ Agent failed with exit code {result.returncode}")
            print(f"STDERR: {result.stderr}")
            return {"status": "error", "message": result.stderr}

        output = json.loads(result.stdout)
        print(f"✅ Agent succeeded")
        print(f"Output: {json.dumps(output, indent=2)}")
        return output

    except subprocess.TimeoutExpired:
        print(f"❌ Agent timed out")
        return {"status": "error", "message": "Timeout"}
    except json.JSONDecodeError as e:
        print(f"❌ Invalid JSON output: {e}")
        print(f"Raw output: {result.stdout}")
        return {"status": "error", "message": "Invalid JSON"}
    except Exception as e:
        print(f"❌ Exception: {e}")
        return {"status": "error", "message": str(e)}


def main():
    print("🧪 SVN to GitHub Migration Agents Test Suite")
    print("=" * 60)

    # Test 1: Repository Analyzer
    analyzer_config = {
        "svn": {
            "url": "https://svn.apache.org/repos/asf/commons/proper/math",
            "trunk_path": "trunk",
            "branches_path": "branches",
            "tags_path": "tags"
        }
    }
    analyzer_result = test_agent("repository_analyzer", analyzer_config)

    # Test 2: Author Mapper
    mapper_config = {
        "svn": {
            "url": "https://svn.apache.org/repos/asf/commons/proper/math"
        },
        "output": {
            "authors_file": "D:\\Svn2Github\\temp\\test_authors.txt"
        }
    }
    mapper_result = test_agent("author_mapper", mapper_config)

    # Test 3: Migration Executor (dry run)
    executor_config = {
        "svn": {
            "url": "https://svn.apache.org/repos/asf/commons/proper/math/trunk",
            "trunk_path": "trunk",
            "branches_path": "branches",
            "tags_path": "tags",
            "authors_file": mapper_result.get("authors_file", "")
        },
        "output": {
            "directory": "D:\\Svn2Github\\temp\\test_repo"
        }
    }
    # Note: Don't actually run this as it takes hours
    print(f"\n{'='*60}")
    print("⏭️  Skipping migration_executor (takes too long)")
    print(f"Config would be: {json.dumps(executor_config, indent=2)}")

    # Summary
    print(f"\n{'='*60}")
    print("📊 Test Summary")
    print(f"{'='*60}")
    print(f"Repository Analyzer: {'✅' if analyzer_result.get('status') != 'error' else '❌'}")
    print(f"Author Mapper: {'✅' if mapper_result.get('status') != 'error' else '❌'}")
    print("Migration Executor: ⏭️  (skipped)")


if __name__ == "__main__":
    main()
