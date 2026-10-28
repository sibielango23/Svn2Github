"""
Audit trail endpoints for the API.
"""

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session
from typing import Optional
from datetime import datetime, timedelta

from api.database import get_db, AuditTrail
from api.schemas import AuditResponse, AuditCreate

router = APIRouter()


@router.get("", response_model=list[AuditResponse])
async def get_audit_trail(
    skip: int = Query(0, ge=0),
    limit: int = Query(100, ge=1, le=1000),
    migration_id: Optional[str] = None,
    action: Optional[str] = None,
    user: Optional[str] = None,
    days: Optional[int] = Query(None, ge=1, le=365, description="Filter by last N days"),
    db: Session = Depends(get_db)
):
    """Get audit trail with filtering."""
    query = db.query(AuditTrail)

    # Apply filters
    if migration_id:
        query = query.filter(AuditTrail.migration_id == migration_id)

    if action:
        query = query.filter(AuditTrail.action == action)

    if user:
        query = query.filter(AuditTrail.user == user)

    if days:
        since = datetime.utcnow() - timedelta(days=days)
        query = query.filter(AuditTrail.timestamp >= since)

    # Order by timestamp descending
    query = query.order_by(AuditTrail.timestamp.desc())

    # Apply pagination
    entries = query.offset(skip).limit(limit).all()

    return entries


@router.get("/actions")
async def get_audit_actions(db: Session = Depends(get_db)):
    """Get list of all unique action types."""
    actions = db.query(AuditTrail.action).distinct().all()
    return {"actions": [action[0] for action in actions if action[0]]}


@router.get("/users")
async def get_audit_users(db: Session = Depends(get_db)):
    """Get list of all unique users."""
    users = db.query(AuditTrail.user).distinct().all()
    return {"users": [user[0] for user in users if user[0]]}


@router.post("", response_model=AuditResponse, status_code=201)
async def create_audit_entry(entry: AuditCreate, db: Session = Depends(get_db)):
    """Create a new audit entry."""
    import json

    new_entry = AuditTrail(
        migration_id=entry.migration_id,
        action=entry.action,
        user=entry.user,
        details_json=json.dumps(entry.details) if entry.details else None
    )

    db.add(new_entry)
    db.commit()
    db.refresh(new_entry)

    return new_entry
