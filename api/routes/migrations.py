"""
Migration endpoints for the API.
"""

from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks, WebSocket, WebSocketDisconnect
from sqlalchemy.orm import Session
from typing import List, Optional
import uuid
import json
from datetime import datetime
import asyncio

from api.database import get_db, Migration, Log, AuditTrail
from api.schemas import (
    MigrationConfigCreate, MigrationResponse, MigrationListResponse,
    MigrationStatus, LogStreamMessage, StatusUpdateMessage
)
from api.orchestrator_wrapper import MigrationOrchestrator

router = APIRouter()

# Active WebSocket connections for real-time updates
active_connections: dict = {}


class ConnectionManager:
    """Manages WebSocket connections for real-time updates."""

    def __init__(self):
        self.active_connections: dict[str, List[WebSocket]] = {}

    async def connect(self, migration_id: str, websocket: WebSocket):
        await websocket.accept()
        if migration_id not in self.active_connections:
            self.active_connections[migration_id] = []
        self.active_connections[migration_id].append(websocket)

    def disconnect(self, migration_id: str, websocket: WebSocket):
        if migration_id in self.active_connections:
            self.active_connections[migration_id].remove(websocket)
            if not self.active_connections[migration_id]:
                del self.active_connections[migration_id]

    async def broadcast(self, migration_id: str, message: dict):
        """Broadcast message to all connected clients for this migration."""
        if migration_id in self.active_connections:
            for connection in self.active_connections[migration_id]:
                try:
                    await connection.send_json(message)
                except Exception as e:
                    print(f"Error broadcasting to client: {e}")


manager = ConnectionManager()


@router.post("", response_model=MigrationResponse, status_code=201)
async def create_migration(
    config: MigrationConfigCreate,
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db)
):
    """Create and start a new migration."""
    # Generate unique ID
    migration_id = str(uuid.uuid4())

    # Create config JSON
    config_dict = {
        "svn": {
            "url": config.svn_url,
            "structure": config.svn_structure,
            "trunk": config.svn_trunk,
            "branches": config.svn_branches,
            "tags": config.svn_tags
        },
        "github": {
            "org": config.github_org,
            "repo": config.github_repo,
            "private": config.github_private
        },
        "options": {
            "author_domain": config.author_domain,
            "clean_history": config.clean_history,
            "exclude_paths": config.exclude_paths,
            "max_file_size_mb": config.max_file_size_mb
        },
        "workspace": f"./migration-workspace/{migration_id}"
    }

    # Create migration record
    migration = Migration(
        id=migration_id,
        svn_url=config.svn_url,
        github_org=config.github_org,
        github_repo=config.github_repo,
        status=MigrationStatus.PENDING.value,
        config_json=json.dumps(config_dict),
        workspace_path=config_dict["workspace"]
    )

    db.add(migration)

    # Create audit entry
    audit = AuditTrail(
        migration_id=migration_id,
        action="migration_created",
        user="api_user",
        details_json=json.dumps({"config": config_dict})
    )
    db.add(audit)

    # Create initial log
    log = Log(
        migration_id=migration_id,
        level="INFO",
        agent="api",
        message="Migration created and queued"
    )
    db.add(log)

    db.commit()
    db.refresh(migration)

    # Start migration in background
    background_tasks.add_task(run_migration, migration_id, config_dict, db)

    return migration


@router.get("", response_model=MigrationListResponse)
async def list_migrations(
    skip: int = 0,
    limit: int = 100,
    status: Optional[str] = None,
    db: Session = Depends(get_db)
):
    """List all migrations with optional filtering."""
    query = db.query(Migration)

    if status:
        query = query.filter(Migration.status == status)

    total = query.count()
    migrations = query.offset(skip).limit(limit).all()

    return MigrationListResponse(
        migrations=migrations,
        total=total,
        page=skip // limit + 1,
        page_size=limit
    )


@router.get("/{migration_id}", response_model=MigrationResponse)
async def get_migration(migration_id: str, db: Session = Depends(get_db)):
    """Get a specific migration by ID."""
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")
    return migration


@router.get("/{migration_id}/status")
async def get_migration_status(migration_id: str, db: Session = Depends(get_db)):
    """Get real-time status of a migration."""
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    return {
        "id": migration.id,
        "status": migration.status,
        "current_phase": migration.current_phase,
        "progress_percent": migration.progress_percent,
        "processed_revisions": migration.processed_revisions,
        "total_revisions": migration.total_revisions,
        "error_message": migration.error_message
    }


@router.post("/{migration_id}/pause")
async def pause_migration(migration_id: str, db: Session = Depends(get_db)):
    """Pause a running migration."""
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    if migration.status != MigrationStatus.RUNNING.value:
        raise HTTPException(status_code=400, detail="Migration is not running")

    migration.status = MigrationStatus.PAUSED.value

    # Create audit entry
    audit = AuditTrail(
        migration_id=migration_id,
        action="migration_paused",
        user="api_user"
    )
    db.add(audit)

    db.commit()

    return {"message": "Migration paused", "status": migration.status}


@router.post("/{migration_id}/resume")
async def resume_migration(
    migration_id: str,
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db)
):
    """Resume a paused migration."""
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    if migration.status != MigrationStatus.PAUSED.value:
        raise HTTPException(status_code=400, detail="Migration is not paused")

    migration.status = MigrationStatus.RUNNING.value

    # Create audit entry
    audit = AuditTrail(
        migration_id=migration_id,
        action="migration_resumed",
        user="api_user"
    )
    db.add(audit)

    db.commit()

    # Resume in background
    config_dict = json.loads(migration.config_json)
    background_tasks.add_task(run_migration, migration_id, config_dict, db)

    return {"message": "Migration resumed", "status": migration.status}


@router.post("/{migration_id}/stop")
async def stop_migration(migration_id: str, db: Session = Depends(get_db)):
    """Stop a running migration."""
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    if migration.status not in [MigrationStatus.RUNNING.value, MigrationStatus.PAUSED.value]:
        raise HTTPException(status_code=400, detail="Migration is not running or paused")

    migration.status = MigrationStatus.FAILED.value
    migration.error_message = "Migration stopped by user"
    migration.end_time = datetime.utcnow()

    # Create audit entry
    audit = AuditTrail(
        migration_id=migration_id,
        action="migration_stopped",
        user="api_user"
    )
    db.add(audit)

    db.commit()

    return {"message": "Migration stopped", "status": migration.status}


@router.delete("/{migration_id}")
async def delete_migration(migration_id: str, db: Session = Depends(get_db)):
    """Delete a migration record."""
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    if migration.status == MigrationStatus.RUNNING.value:
        raise HTTPException(status_code=400, detail="Cannot delete running migration")

    # Create audit entry before deletion
    audit = AuditTrail(
        migration_id=migration_id,
        action="migration_deleted",
        user="api_user"
    )
    db.add(audit)
    db.commit()

    # Delete migration (cascade will delete related records)
    db.delete(migration)
    db.commit()

    return {"message": "Migration deleted"}


@router.websocket("/{migration_id}/stream")
async def websocket_endpoint(websocket: WebSocket, migration_id: str):
    """WebSocket endpoint for real-time log streaming."""
    await manager.connect(migration_id, websocket)
    try:
        while True:
            # Keep connection alive
            await asyncio.sleep(1)
    except WebSocketDisconnect:
        manager.disconnect(migration_id, websocket)


async def run_migration(migration_id: str, config: dict, db: Session):
    """Run migration in background task."""
    # Update status to running
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    migration.status = MigrationStatus.RUNNING.value
    migration.start_time = datetime.utcnow()
    db.commit()

    try:
        # Create orchestrator wrapper
        orchestrator = MigrationOrchestrator(migration_id, config, db, manager)

        # Run migration
        await orchestrator.execute()

        # Update status to completed
        migration = db.query(Migration).filter(Migration.id == migration_id).first()
        migration.status = MigrationStatus.COMPLETED.value
        migration.end_time = datetime.utcnow()
        migration.progress_percent = 100.0
        db.commit()

    except Exception as e:
        # Update status to failed
        migration = db.query(Migration).filter(Migration.id == migration_id).first()
        migration.status = MigrationStatus.FAILED.value
        migration.error_message = str(e)
        migration.end_time = datetime.utcnow()
        db.commit()

        # Log error
        log = Log(
            migration_id=migration_id,
            level="ERROR",
            agent="orchestrator",
            message=f"Migration failed: {str(e)}"
        )
        db.add(log)
        db.commit()
