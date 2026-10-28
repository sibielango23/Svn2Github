"""
Log endpoints for the API.
"""

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session
from typing import Optional
from datetime import datetime

from api.database import get_db, Log
from api.schemas import LogResponse, LogListResponse, LogLevel

router = APIRouter()


@router.get("/{migration_id}", response_model=LogListResponse)
async def get_logs(
    migration_id: str,
    skip: int = Query(0, ge=0),
    limit: int = Query(100, ge=1, le=1000),
    level: Optional[LogLevel] = None,
    agent: Optional[str] = None,
    search: Optional[str] = None,
    db: Session = Depends(get_db)
):
    """Get logs for a migration with filtering and pagination."""
    query = db.query(Log).filter(Log.migration_id == migration_id)

    # Apply filters
    if level:
        query = query.filter(Log.level == level.value)

    if agent:
        query = query.filter(Log.agent == agent)

    if search:
        query = query.filter(Log.message.contains(search))

    # Order by timestamp descending (newest first)
    query = query.order_by(Log.timestamp.desc())

    # Get total count
    total = query.count()

    # Apply pagination
    logs = query.offset(skip).limit(limit).all()

    return LogListResponse(
        logs=logs,
        total=total,
        page=skip // limit + 1,
        page_size=limit
    )


@router.get("/{migration_id}/export")
async def export_logs(
    migration_id: str,
    format: str = Query("txt", regex="^(txt|json|csv)$"),
    db: Session = Depends(get_db)
):
    """Export logs in various formats."""
    logs = db.query(Log).filter(Log.migration_id == migration_id).order_by(Log.timestamp).all()

    if not logs:
        raise HTTPException(status_code=404, detail="No logs found")

    if format == "json":
        import json
        return {
            "logs": [
                {
                    "timestamp": log.timestamp.isoformat(),
                    "level": log.level,
                    "agent": log.agent,
                    "message": log.message
                }
                for log in logs
            ]
        }

    elif format == "csv":
        import csv
        import io

        output = io.StringIO()
        writer = csv.writer(output)
        writer.writerow(["Timestamp", "Level", "Agent", "Message"])

        for log in logs:
            writer.writerow([
                log.timestamp.isoformat(),
                log.level,
                log.agent or "",
                log.message
            ])

        return {"content": output.getvalue(), "filename": f"migration_{migration_id}_logs.csv"}

    else:  # txt
        lines = []
        for log in logs:
            lines.append(f"[{log.timestamp}] {log.level} - {log.agent or 'system'}: {log.message}")

        return {"content": "\n".join(lines), "filename": f"migration_{migration_id}_logs.txt"}


@router.get("/{migration_id}/summary")
async def get_log_summary(migration_id: str, db: Session = Depends(get_db)):
    """Get log summary statistics."""
    logs = db.query(Log).filter(Log.migration_id == migration_id).all()

    if not logs:
        return {
            "total": 0,
            "by_level": {},
            "by_agent": {},
            "first_log": None,
            "last_log": None
        }

    # Count by level
    by_level = {}
    for log in logs:
        by_level[log.level] = by_level.get(log.level, 0) + 1

    # Count by agent
    by_agent = {}
    for log in logs:
        agent = log.agent or "system"
        by_agent[agent] = by_agent.get(agent, 0) + 1

    return {
        "total": len(logs),
        "by_level": by_level,
        "by_agent": by_agent,
        "first_log": logs[0].timestamp,
        "last_log": logs[-1].timestamp
    }
