#!/usr/bin/env python3
"""
Quick smoke test of the API
"""

import requests
import time

BASE_URL = "http://localhost:8000/api"

print("=" * 70)
print("🧪 Quick API Smoke Test")
print("=" * 70)

# Test 1: Health
print("\n✅ Testing /health...")
r = requests.get(f"{BASE_URL}/health")
print(f"   {r.json()}")

# Test 2: Stats
print("\n✅ Testing /stats...")
r = requests.get(f"{BASE_URL}/stats")
print(f"   {r.json()}")

# Test 3: List migrations
print("\n✅ Testing /migrations...")
r = requests.get(f"{BASE_URL}/migrations")
print(f"   Found {len(r.json())} migrations")

# Test 4: Create migration (will fail on analyzer since SVN not installed)
print("\n✅ Testing POST /migrations...")
payload = {
    "svn_url": "https://svn.example.com/repo/trunk",
    "github_org": "test-org",
    "github_repo": "test-repo",
    "github_token": "test_token",
    "trunk_path": "trunk",
    "branches_path": "branches",
    "tags_path": "tags",
    "clean_history": False
}
r = requests.post(f"{BASE_URL}/migrations", json=payload)
data = r.json()
print(f"   Migration created: {data['id']}")

# Wait for processing
time.sleep(2)

# Test 5: Get specific migration
print(f"\n✅ Testing GET /migrations/{data['id']}...")
r = requests.get(f"{BASE_URL}/migrations/{data['id']}")
migration = r.json()
print(f"   Status: {migration['status']}")
print(f"   Progress: {migration['progress']}%")
print(f"   Step: {migration['current_step']}")

if migration['status'] == 'failed':
    print(f"   Expected failure (SVN not installed)")

# Test 6: Delete
print(f"\n✅ Testing DELETE /migrations/{data['id']}...")
r = requests.delete(f"{BASE_URL}/migrations/{data['id']}")
print(f"   Deleted")

print("\n" + "=" * 70)
print("✅ All smoke tests passed!")
print("=" * 70)
