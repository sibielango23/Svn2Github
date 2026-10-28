"""
Pydantic schemas for request/response validation.
"""

from pydantic import BaseModel, Field, EmailStr
from typing import Optional, List, Dict, Any
from datetime import datetime
from enum import Enum


class MigrationStatus(str, Enum):
    """Migration status enumeration."""
    PENDING = "pending"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    PAUSED = "paused"


class LogLevel(str, Enum):
    """Log level enumeration."""
    DEBUG = "DEBUG"
    INFO = "INFO"
    WARNING = "WARNING"
    ERROR = "ERROR"


class VerificationStatus(str, Enum):
    """Verification status enumeration."""
    PASSED = "passed"
    FAILED = "failed"
    WARNING = "warning"


# Migration Schemas
class MigrationConfigCreate(BaseModel):
    """Schema for creating a new migration."""
    svn_url: str = Field(..., description="SVN repository URL")
    github_org: str = Field(..., description="GitHub organization")
    github_repo: str = Field(..., description="GitHub repository name")
    svn_structure: str = Field(default="standard", description="SVN structure (standard/non-standard)")
    svn_trunk: Optional[str] = Field(default="trunk", description="Trunk path")
    svn_branches: Optional[str] = Field(default="branches", description="Branches path")
    svn_tags: Optional[str] = Field(default="tags", description="Tags path")
    github_private: bool = Field(default=True, description="Make GitHub repo private")
    author_domain: str = Field(default="example.com", description="Default email domain for authors")
    clean_history: bool = Field(default=False, description="Clean repository history")
    exclude_paths: Optional[List[str]] = Field(default=[], description="Patterns to exclude")
    max_file_size_mb: int = Field(default=100, description="Maximum file size in MB")


class MigrationResponse(BaseModel):
    """Schema for migration response."""
    id: str
    svn_url: str
    github_org: str
    github_repo: str
    status: MigrationStatus
    start_time: Optional[datetime]
    end_time: Optional[datetime]
    current_phase: Optional[str]
    progress_percent: float
    total_revisions: Optional[int]
    processed_revisions: int
    error_message: Optional[str]
    workspace_path: Optional[str]

    class Config:
        from_attributes = True


class MigrationListResponse(BaseModel):
    """Schema for list of migrations."""
    migrations: List[MigrationResponse]
    total: int
    page: int
    page_size: int


# Log Schemas
class LogCreate(BaseModel):
    """Schema for creating a log entry."""
    migration_id: str
    level: LogLevel
    agent: Optional[str]
    message: str
    details: Optional[Dict[str, Any]] = None


class LogResponse(BaseModel):
    """Schema for log response."""
    id: int
    migration_id: str
    timestamp: datetime
    level: str
    agent: Optional[str]
    message: str
    details_json: Optional[str]

    class Config:
        from_attributes = True


class LogListResponse(BaseModel):
    """Schema for paginated logs."""
    logs: List[LogResponse]
    total: int
    page: int
    page_size: int


# Author Schemas
class AuthorCreate(BaseModel):
    """Schema for creating/updating an author."""
    svn_username: str
    git_name: Optional[str]
    git_email: Optional[str]
    verified: bool = False


class AuthorResponse(BaseModel):
    """Schema for author response."""
    id: int
    migration_id: str
    svn_username: str
    git_name: Optional[str]
    git_email: Optional[str]
    commit_count: int
    verified: bool

    class Config:
        from_attributes = True


class AuthorBulkUpdate(BaseModel):
    """Schema for bulk author updates."""
    authors: List[AuthorCreate]


# Audit Schemas
class AuditCreate(BaseModel):
    """Schema for creating an audit entry."""
    migration_id: Optional[str]
    action: str
    user: Optional[str]
    details: Optional[Dict[str, Any]] = None


class AuditResponse(BaseModel):
    """Schema for audit response."""
    id: int
    timestamp: datetime
    migration_id: Optional[str]
    action: str
    user: Optional[str]
    details_json: Optional[str]

    class Config:
        from_attributes = True


# Verification Schemas
class VerificationCreate(BaseModel):
    """Schema for creating a verification result."""
    migration_id: str
    check_name: str
    status: VerificationStatus
    expected_value: Optional[str]
    actual_value: Optional[str]
    message: Optional[str]


class VerificationResponse(BaseModel):
    """Schema for verification response."""
    id: int
    migration_id: str
    check_name: str
    status: str
    expected_value: Optional[str]
    actual_value: Optional[str]
    message: Optional[str]
    timestamp: datetime

    class Config:
        from_attributes = True


# Statistics Schemas
class StatisticCreate(BaseModel):
    """Schema for creating a statistic."""
    migration_id: str
    metric_name: str
    metric_value: str
    phase: Optional[str]


class StatisticResponse(BaseModel):
    """Schema for statistic response."""
    id: int
    migration_id: str
    metric_name: str
    metric_value: str
    phase: Optional[str]
    timestamp: datetime

    class Config:
        from_attributes = True


# WebSocket Message Schemas
class LogStreamMessage(BaseModel):
    """Schema for real-time log streaming."""
    timestamp: datetime
    level: str
    agent: Optional[str]
    message: str
    progress: Optional[float] = None


class StatusUpdateMessage(BaseModel):
    """Schema for status updates."""
    migration_id: str
    status: MigrationStatus
    current_phase: Optional[str]
    progress_percent: float
    processed_revisions: int
    total_revisions: Optional[int]


# Configuration Test Schemas
class SVNConnectionTest(BaseModel):
    """Schema for testing SVN connection."""
    svn_url: str


class GitHubConnectionTest(BaseModel):
    """Schema for testing GitHub connection."""
    github_org: str
    github_repo: str


class ConnectionTestResponse(BaseModel):
    """Schema for connection test response."""
    success: bool
    message: str
    details: Optional[Dict[str, Any]] = None


# Export Schemas
class ExportFormat(str, Enum):
    """Export format enumeration."""
    JSON = "json"
    CSV = "csv"
    PDF = "pdf"
    TXT = "txt"


class ExportRequest(BaseModel):
    """Schema for export request."""
    format: ExportFormat
    include_details: bool = False
