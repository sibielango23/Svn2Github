"""
Database models and setup for SVN to GitHub migration system.
Uses SQLite for storage.
"""

from sqlalchemy import create_engine, Column, String, Integer, Float, DateTime, Boolean, Text, ForeignKey
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker, relationship
from datetime import datetime
import os

Base = declarative_base()

# Database file location
DB_FILE = os.path.join(os.path.dirname(__file__), '..', 'data', 'migration.db')
os.makedirs(os.path.dirname(DB_FILE), exist_ok=True)

# Create engine
DATABASE_URL = f"sqlite:///{DB_FILE}"
engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})

# Session factory
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)


class Migration(Base):
    """Main migration tracking table."""
    __tablename__ = "migrations"

    id = Column(String, primary_key=True)
    svn_url = Column(String, nullable=False)
    github_org = Column(String, nullable=False)
    github_repo = Column(String, nullable=False)
    status = Column(String, nullable=False, default="pending")  # pending, running, completed, failed, paused
    start_time = Column(DateTime, nullable=True)
    end_time = Column(DateTime, nullable=True)
    config_json = Column(Text, nullable=True)
    current_phase = Column(String, nullable=True)
    progress_percent = Column(Float, default=0.0)
    total_revisions = Column(Integer, nullable=True)
    processed_revisions = Column(Integer, default=0)
    error_message = Column(Text, nullable=True)
    workspace_path = Column(String, nullable=True)

    # Relationships
    logs = relationship("Log", back_populates="migration", cascade="all, delete-orphan")
    authors = relationship("Author", back_populates="migration", cascade="all, delete-orphan")
    audit_entries = relationship("AuditTrail", back_populates="migration", cascade="all, delete-orphan")
    verification_results = relationship("VerificationResult", back_populates="migration", cascade="all, delete-orphan")
    statistics = relationship("Statistic", back_populates="migration", cascade="all, delete-orphan")


class Log(Base):
    """Log entries for migrations."""
    __tablename__ = "logs"

    id = Column(Integer, primary_key=True, autoincrement=True)
    migration_id = Column(String, ForeignKey("migrations.id"), nullable=False)
    timestamp = Column(DateTime, default=datetime.utcnow, nullable=False)
    level = Column(String, nullable=False)  # INFO, WARNING, ERROR, DEBUG
    agent = Column(String, nullable=True)
    message = Column(Text, nullable=False)
    details_json = Column(Text, nullable=True)

    # Relationship
    migration = relationship("Migration", back_populates="logs")


class Author(Base):
    """Author mappings for migrations."""
    __tablename__ = "authors"

    id = Column(Integer, primary_key=True, autoincrement=True)
    migration_id = Column(String, ForeignKey("migrations.id"), nullable=False)
    svn_username = Column(String, nullable=False)
    git_name = Column(String, nullable=True)
    git_email = Column(String, nullable=True)
    commit_count = Column(Integer, default=0)
    verified = Column(Boolean, default=False)

    # Relationship
    migration = relationship("Migration", back_populates="authors")


class AuditTrail(Base):
    """Audit trail for all actions."""
    __tablename__ = "audit_trail"

    id = Column(Integer, primary_key=True, autoincrement=True)
    timestamp = Column(DateTime, default=datetime.utcnow, nullable=False)
    migration_id = Column(String, ForeignKey("migrations.id"), nullable=True)
    action = Column(String, nullable=False)  # start, stop, pause, config_change, etc.
    user = Column(String, nullable=True)
    details_json = Column(Text, nullable=True)

    # Relationship
    migration = relationship("Migration", back_populates="audit_entries")


class VerificationResult(Base):
    """Verification check results."""
    __tablename__ = "verification_results"

    id = Column(Integer, primary_key=True, autoincrement=True)
    migration_id = Column(String, ForeignKey("migrations.id"), nullable=False)
    check_name = Column(String, nullable=False)
    status = Column(String, nullable=False)  # passed, failed, warning
    expected_value = Column(String, nullable=True)
    actual_value = Column(String, nullable=True)
    message = Column(Text, nullable=True)
    timestamp = Column(DateTime, default=datetime.utcnow, nullable=False)

    # Relationship
    migration = relationship("Migration", back_populates="verification_results")


class Statistic(Base):
    """Migration statistics."""
    __tablename__ = "statistics"

    id = Column(Integer, primary_key=True, autoincrement=True)
    migration_id = Column(String, ForeignKey("migrations.id"), nullable=False)
    metric_name = Column(String, nullable=False)  # commits, branches, tags, size_mb, etc.
    metric_value = Column(String, nullable=True)
    phase = Column(String, nullable=True)  # analysis, migration, verification
    timestamp = Column(DateTime, default=datetime.utcnow, nullable=False)

    # Relationship
    migration = relationship("Migration", back_populates="statistics")


def init_db():
    """Initialize database, create all tables."""
    Base.metadata.create_all(bind=engine)


def get_db():
    """Dependency for getting database session."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


if __name__ == "__main__":
    print(f"Creating database at: {DB_FILE}")
    init_db()
    print("Database initialized successfully!")
