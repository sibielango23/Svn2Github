#!/usr/bin/env python3
"""
SVN to GitHub Migration API Server
Provides REST API for the WPF frontend application
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import json
import os
import sys
import subprocess
from datetime import datetime
from pathlib import Path

app = Flask(__name__)
CORS(app)

# Configuration
AGENTS_DIR = Path(__file__).parent / "agents"
DATA_DIR = Path(__file__).parent / "data"
MIGRATIONS_FILE = DATA_DIR / "migrations.json"

# Ensure data directory exists
DATA_DIR.mkdir(exist_ok=True)

def load_migrations():
    """Load migrations from JSON file"""
    if MIGRATIONS_FILE.exists():
        with open(MIGRATIONS_FILE, 'r') as f:
            return json.load(f)
    return []

def save_migrations(migrations):
    """Save migrations to JSON file"""
    with open(MIGRATIONS_FILE, 'w') as f:
        json.dump(migrations, f, indent=2)

@app.route('/api/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        "status": "healthy",
        "timestamp": datetime.now().isoformat(),
        "version": "1.0.0"
    })

@app.route('/api/migrations', methods=['GET'])
def get_migrations():
    """Get all migrations"""
    migrations = load_migrations()
    return jsonify(migrations)

@app.route('/api/migrations', methods=['POST'])
def create_migration():
    """Create a new migration"""
    data = request.json

    # Validate required fields
    required_fields = ['svn_url', 'github_org', 'github_repo']
    missing_fields = [f for f in required_fields if f not in data]
    if missing_fields:
        return jsonify({'error': f'Missing fields: {", ".join(missing_fields)}'}), 400

    migrations = load_migrations()

    # Create new migration
    migration = {
        'id': len(migrations) + 1,
        'svn_url': data['svn_url'],
        'github_org': data['github_org'],
        'github_repo': data['github_repo'],
        'status': 'pending',
        'created_at': datetime.now().isoformat(),
        'updated_at': datetime.now().isoformat(),
        'progress': 0,
        'current_phase': 'Initializing',
        'logs': []
    }

    migrations.append(migration)
    save_migrations(migrations)

    return jsonify(migration), 201

@app.route('/api/migrations/<int:migration_id>', methods=['GET'])
def get_migration(migration_id):
    """Get specific migration"""
    migrations = load_migrations()
    migration = next((m for m in migrations if m['id'] == migration_id), None)

    if not migration:
        return jsonify({'error': 'Migration not found'}), 404

    return jsonify(migration)

@app.route('/api/migrations/<int:migration_id>', methods=['PUT'])
def update_migration(migration_id):
    """Update migration status"""
    migrations = load_migrations()
    migration = next((m for m in migrations if m['id'] == migration_id), None)

    if not migration:
        return jsonify({'error': 'Migration not found'}), 404

    data = request.json
    migration.update(data)
    migration['updated_at'] = datetime.now().isoformat()

    save_migrations(migrations)
    return jsonify(migration)

@app.route('/api/migrations/<int:migration_id>', methods=['DELETE'])
def delete_migration(migration_id):
    """Delete a migration"""
    migrations = load_migrations()
    migration = next((m for m in migrations if m['id'] == migration_id), None)

    if not migration:
        return jsonify({'error': 'Migration not found'}), 404

    migrations = [m for m in migrations if m['id'] != migration_id]
    save_migrations(migrations)

    return '', 204

@app.route('/api/migrations/<int:migration_id>/start', methods=['POST'])
def start_migration(migration_id):
    """Start a migration"""
    migrations = load_migrations()
    migration = next((m for m in migrations if m['id'] == migration_id), None)

    if not migration:
        return jsonify({'error': 'Migration not found'}), 404

    # Update status to running
    migration['status'] = 'running'
    migration['updated_at'] = datetime.now().isoformat()
    save_migrations(migrations)

    # TODO: Start orchestrator in background
    # This would trigger the agents/orchestrator.py

    return jsonify(migration)

@app.route('/api/authors', methods=['GET'])
def get_authors():
    """Get author mappings"""
    authors_file = DATA_DIR / "authors.json"
    if authors_file.exists():
        with open(authors_file, 'r') as f:
            return jsonify(json.load(f))
    return jsonify([])

@app.route('/api/authors', methods=['POST'])
def save_authors():
    """Save author mappings"""
    authors = request.json
    authors_file = DATA_DIR / "authors.json"
    with open(authors_file, 'w') as f:
        json.dump(authors, f, indent=2)
    return jsonify(authors)

if __name__ == '__main__':
    print("Starting SVN to GitHub Migration API Server...")
    print(f"Data directory: {DATA_DIR.absolute()}")
    print(f"Agents directory: {AGENTS_DIR.absolute()}")
    print(f"Listening on http://localhost:5000")
    print("Press Ctrl+C to stop")

    app.run(host='127.0.0.1', port=5000, debug=True)
