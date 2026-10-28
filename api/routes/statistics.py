"""
Statistics endpoints for the API.
"""

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from typing import List, Optional

from api.database import get_db, Statistic, Migration
from api.schemas import StatisticResponse, StatisticCreate

router = APIRouter()


@router.get("/{migration_id}", response_model=List[StatisticResponse])
async def get_statistics(
    migration_id: str,
    phase: Optional[str] = None,
    db: Session = Depends(get_db)
):
    """Get statistics for a migration."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    query = db.query(Statistic).filter(Statistic.migration_id == migration_id)

    if phase:
        query = query.filter(Statistic.phase == phase)

    statistics = query.order_by(Statistic.timestamp).all()
    return statistics


@router.get("/{migration_id}/summary")
async def get_statistics_summary(migration_id: str, db: Session = Depends(get_db)):
    """Get summarized statistics for a migration."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    statistics = db.query(Statistic).filter(Statistic.migration_id == migration_id).all()

    # Group by phase
    by_phase = {}
    for stat in statistics:
        phase = stat.phase or "general"
        if phase not in by_phase:
            by_phase[phase] = {}
        by_phase[phase][stat.metric_name] = stat.metric_value

    return {
        "migration_id": migration_id,
        "by_phase": by_phase,
        "total_statistics": len(statistics)
    }


@router.post("/{migration_id}", response_model=StatisticResponse, status_code=201)
async def create_statistic(
    migration_id: str,
    statistic: StatisticCreate,
    db: Session = Depends(get_db)
):
    """Create a new statistic entry."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    new_statistic = Statistic(
        migration_id=migration_id,
        metric_name=statistic.metric_name,
        metric_value=statistic.metric_value,
        phase=statistic.phase
    )

    db.add(new_statistic)
    db.commit()
    db.refresh(new_statistic)

    return new_statistic
