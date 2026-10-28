"""Shared route initialization."""

from api.routes import migrations, logs, authors, config, statistics, verification, audit

__all__ = [
    'migrations',
    'logs',
    'authors',
    'config',
    'statistics',
    'verification',
    'audit'
]
