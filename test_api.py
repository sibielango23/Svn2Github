#!/usr/bin/env python3
"""
Integration test for the API server
"""

import requests
import time
import json
import sys

BASE_URL = "http://localhost:8000/api"

def test_health():
    """Test health endpoint"""
    print("\n🔍 Testing Health Endpoint...")
    response = requests.get(f"{BASE_URL}/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    print(f"✅ Health check passed: {data}")

def test_stats():
    """Test stats endpoint"""
    print("\n🔍 Testing Stats Endpoint...")
    response = requests.get(f"{BASE_URL}/stats")
    assert response.status_code == 200
    data = response.json()
    assert "total" in data
    assert "active" in data
    print(f"✅ Stats: {data}")

def test_list_migrations():
    """Test listing migrations"""
    print("\n🔍 Testing List Migrations...")
    response = requests.get(f"{BASE_URL}/migrations")
    assert response.status_code == 200
    data = response.json()
    print(f"✅ Found {len(data)} migrations")
    return data

def test_create_migration():
    """Test creating a new migration"""
    print("\n🔍 Testing Create Migration...")

    payload = {
        "svn_url": "https://svn.apache.org/repos/asf/commons/proper/math/trunk",
        "github_org": "test-org",
        "github_repo": "test-math-migration",
        "github_token": "test_token_12345",
        "trunk_path": "trunk",
        "branches_path": "branches",
        "tags_path": "tags",
        "clean_history": False
    }

    response = requests.post(f"{BASE_URL}/migrations", json=payload)
    assert response.status_code == 200
    data = response.json()
    assert "id" in data
    migration_id = data["id"]
    print(f"✅ Migration created: {migration_id}")
    return migration_id

def test_get_migration(migration_id):
    """Test getting migration details"""
    print(f"\n🔍 Testing Get Migration: {migration_id}")

    # Wait a bit for processing
    time.sleep(2)

    response = requests.get(f"{BASE_URL}/migrations/{migration_id}")
    assert response.status_code == 200
    data = response.json()
    print(f"✅ Migration status: {data['status']}")
    print(f"   Progress: {data['progress']}%")
    print(f"   Current step: {data['current_step']}")
    if data.get("logs"):
        print(f"   Latest log: {data['logs'][-1][:100]}...")

    return data

def test_delete_migration(migration_id):
    """Test deleting a migration"""
    print(f"\n🔍 Testing Delete Migration: {migration_id}")
    response = requests.delete(f"{BASE_URL}/migrations/{migration_id}")
    assert response.status_code == 200
    print("✅ Migration deleted")

def main():
    print("=" * 70)
    print("🧪 SVN to GitHub Migration API Integration Tests")
    print("=" * 70)

    try:
        # Test basic endpoints
        test_health()
        test_stats()
        test_list_migrations()

        # Test migration workflow
        migration_id = test_create_migration()
        migration = test_get_migration(migration_id)

        # Check if migration started or failed
        if migration["status"] == "failed":
            print(f"\n⚠️  Migration failed (expected for test):")
            if migration.get("error"):
                print(f"   Error: {migration['error'][:200]}...")

        # Cleanup
        test_delete_migration(migration_id)

        # Final stats
        print("\n" + "=" * 70)
        print("📊 Final Summary")
        print("=" * 70)
        test_stats()

        print("\n✅ All API tests passed!")
        return 0

    except AssertionError as e:
        print(f"\n❌ Test failed: {e}")
        return 1
    except requests.exceptions.ConnectionError:
        print(f"\n❌ Cannot connect to API at {BASE_URL}")
        print("   Make sure the API server is running (python api/main.py)")
        return 1
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        return 1

if __name__ == "__main__":
    sys.exit(main())
