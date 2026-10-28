"""
Verification endpoints for the API.
"""

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from typing import List

from api.database import get_db, VerificationResult, Migration
from api.schemas import VerificationResponse, VerificationCreate

router = APIRouter()


@router.get("/{migration_id}", response_model=List[VerificationResponse])
async def get_verification_results(migration_id: str, db: Session = Depends(get_db)):
    """Get verification results for a migration."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    results = db.query(VerificationResult).filter(
        VerificationResult.migration_id == migration_id
    ).order_by(VerificationResult.timestamp.desc()).all()

    return results


@router.get("/{migration_id}/summary")
async def get_verification_summary(migration_id: str, db: Session = Depends(get_db)):
    """Get verification summary for a migration."""
    results = db.query(VerificationResult).filter(
        VerificationResult.migration_id == migration_id
    ).all()

    if not results:
        return {
            "migration_id": migration_id,
            "total_checks": 0,
            "passed": 0,
            "failed": 0,
            "warnings": 0,
            "pass_rate": 0.0
        }

    passed = sum(1 for r in results if r.status == "passed")
    failed = sum(1 for r in results if r.status == "failed")
    warnings = sum(1 for r in results if r.status == "warning")

    return {
        "migration_id": migration_id,
        "total_checks": len(results),
        "passed": passed,
        "failed": failed,
        "warnings": warnings,
        "pass_rate": (passed / len(results)) * 100 if results else 0.0
    }


@router.post("/{migration_id}", response_model=VerificationResponse, status_code=201)
async def create_verification_result(
    migration_id: str,
    verification: VerificationCreate,
    db: Session = Depends(get_db)
):
    """Create a new verification result."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    new_result = VerificationResult(
        migration_id=migration_id,
        check_name=verification.check_name,
        status=verification.status.value,
        expected_value=verification.expected_value,
        actual_value=verification.actual_value,
        message=verification.message
    )

    db.add(new_result)
    db.commit()
    db.refresh(new_result)

    return new_result
