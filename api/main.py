from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional, List, Dict
from datetime import datetime
from enum import Enum
import uuid
import asyncio
import subprocess
import json
import os

app = FastAPI(title="SVN to GitHub Migration API")

# CORS middleware for WPF app
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# In-memory storage (replace with database in production)
migrations: Dict[str, dict] = {}
authors: List[Dict[str, str]] = []

class MigrationStatus(str, Enum):
    PENDING = "pending"
    ANALYZING = "analyzing"
    MAPPING_AUTHORS = "mapping_authors"
    MIGRATING = "migrating"
    CLEANING = "cleaning"
    PUSHING = "pushing"
    COMPLETED = "completed"
    FAILED = "failed"

class MigrationRequest(BaseModel):
    svn_url: str
    github_org: str
    github_repo: str
    github_token: str
    authors_file: Optional[str] = None
    clean_history: bool = False
    trunk_path: str = "trunk"
    branches_path: str = "branches"
    tags_path: str = "tags"

class MigrationResponse(BaseModel):
    id: str
    status: MigrationStatus
    message: str
    created_at: str

class MigrationDetail(BaseModel):
    id: str
    svn_url: str
    github_org: str
    github_repo: str
    status: MigrationStatus
    progress: int  # 0-100
    current_step: str
    logs: List[str]
    created_at: str
    updated_at: str
    error: Optional[str] = None


@app.get("/")
async def root():
    return {"message": "SVN to GitHub Migration API", "version": "1.0.0"}

@app.get("/api/health")
async def health_check():
    return {"status": "healthy", "timestamp": datetime.utcnow().isoformat()}

@app.post("/api/migrations", response_model=MigrationResponse)
async def create_migration(request: MigrationRequest, background_tasks: BackgroundTasks):
    """Create a new migration job"""
    migration_id = str(uuid.uuid4())

    migration_data = {
        "id": migration_id,
        "svn_url": request.svn_url,
        "github_org": request.github_org,
        "github_repo": request.github_repo,
        "github_token": request.github_token,
        "authors_file": request.authors_file,
        "clean_history": request.clean_history,
        "trunk_path": request.trunk_path,
        "branches_path": request.branches_path,
        "tags_path": request.tags_path,
        "status": MigrationStatus.PENDING,
        "progress": 0,
        "current_step": "Queued",
        "logs": ["Migration created"],
        "created_at": datetime.utcnow().isoformat(),
        "updated_at": datetime.utcnow().isoformat(),
        "error": None
    }

    migrations[migration_id] = migration_data

    # Start migration in background
    background_tasks.add_task(run_migration, migration_id)

    return MigrationResponse(
        id=migration_id,
        status=MigrationStatus.PENDING,
        message="Migration job created successfully",
        created_at=migration_data["created_at"]
    )


@app.get("/api/migrations", response_model=List[MigrationDetail])
async def list_migrations():
    """List all migrations"""
    return [MigrationDetail(**m) for m in migrations.values()]

@app.get("/api/migrations/{migration_id}", response_model=MigrationDetail)
async def get_migration(migration_id: str):
    """Get migration details by ID"""
    if migration_id not in migrations:
        raise HTTPException(status_code=404, detail="Migration not found")
    return MigrationDetail(**migrations[migration_id])

@app.delete("/api/migrations/{migration_id}")
async def cancel_migration(migration_id: str):
    """Cancel a migration job"""
    if migration_id not in migrations:
        raise HTTPException(status_code=404, detail="Migration not found")

    migration = migrations[migration_id]
    if migration["status"] in [MigrationStatus.COMPLETED, MigrationStatus.FAILED]:
        raise HTTPException(status_code=400, detail="Cannot cancel completed or failed migration")

    migration["status"] = MigrationStatus.FAILED
    migration["error"] = "Cancelled by user"
    migration["updated_at"] = datetime.utcnow().isoformat()

    return {"message": "Migration cancelled"}

@app.get("/api/stats")
async def get_stats():
    """Get migration statistics"""
    total = len(migrations)
    active = sum(1 for m in migrations.values()
                 if m["status"] not in [MigrationStatus.COMPLETED, MigrationStatus.FAILED])
    completed = sum(1 for m in migrations.values()
                    if m["status"] == MigrationStatus.COMPLETED)
    failed = sum(1 for m in migrations.values()
                 if m["status"] == MigrationStatus.FAILED)

    return {
        "total": total,
        "active": active,
        "completed": completed,
        "failed": failed
    }

@app.get("/api/authors")
async def get_authors():
    """Get all author mappings"""
    return authors

@app.post("/api/authors")
async def save_authors(author_list: List[Dict[str, str]]):
    """Save author mappings"""
    global authors
    authors = author_list
    return authors


async def run_migration(migration_id: str):
    """Background task to run the complete migration workflow"""
    migration = migrations[migration_id]

    def update_status(status: MigrationStatus, step: str, progress: int, log: str):
        migration["status"] = status
        migration["current_step"] = step
        migration["progress"] = progress
        migration["logs"].append(f"{datetime.utcnow().isoformat()}: {log}")
        migration["updated_at"] = datetime.utcnow().isoformat()

    def run_agent(agent_name: str, config: dict) -> dict:
        """Run an agent and return its output"""
        try:
            agent_path = f"D:\\Svn2Github\\agents\\{agent_name}.py"
            result = subprocess.run(
                ["python", agent_path],
                input=json.dumps(config),
                capture_output=True,
                text=True,
                timeout=3600  # 1 hour timeout per agent
            )

            if result.returncode != 0:
                raise Exception(f"Agent failed: {result.stderr}")

            return json.loads(result.stdout)
        except Exception as e:
            raise Exception(f"Agent {agent_name} error: {str(e)}")

    try:
        # Step 1: Analyze Repository (10%)
        update_status(MigrationStatus.ANALYZING, "Analyzing SVN repository", 10,
                     "Starting repository analysis...")

        analyzer_config = {
            "svn": {
                "url": migration["svn_url"],
                "trunk_path": migration["trunk_path"],
                "branches_path": migration["branches_path"],
                "tags_path": migration["tags_path"]
            },
            "workspace": f"D:\\Svn2Github\\temp\\workspace_{migration_id}"
        }
        analyzer_result = run_agent("repository_analyzer", analyzer_config)
        update_status(MigrationStatus.ANALYZING, "Repository analysis complete", 20,
                     f"Found {analyzer_result.get('total_revisions', 0)} revisions")

        # Step 2: Map Authors (30%)
        update_status(MigrationStatus.MAPPING_AUTHORS, "Mapping SVN authors", 30,
                     "Generating author mappings...")

        mapper_config = {
            "svn": {
                "url": migration["svn_url"]
            },
            "output": {
                "authors_file": f"D:\\Svn2Github\\temp\\authors_{migration_id}.txt"
            },
            "workspace": f"D:\\Svn2Github\\temp\\workspace_{migration_id}"
        }
        mapper_result = run_agent("author_mapper", mapper_config)
        update_status(MigrationStatus.MAPPING_AUTHORS, "Author mapping complete", 40,
                     f"Mapped {mapper_result.get('author_count', 0)} authors")

        # Step 3: Execute Migration (70%)
        update_status(MigrationStatus.MIGRATING, "Migrating SVN to Git", 50,
                     "Starting git-svn clone (this may take hours)...")

        executor_config = {
            "svn": {
                "url": migration["svn_url"],
                "trunk_path": migration["trunk_path"],
                "branches_path": migration["branches_path"],
                "tags_path": migration["tags_path"],
                "authors_file": mapper_result.get("authors_file")
            },
            "output": {
                "directory": f"D:\\Svn2Github\\temp\\repo_{migration_id}"
            }
        }
        executor_result = run_agent("migration_executor", executor_config)
        update_status(MigrationStatus.MIGRATING, "Migration complete", 70,
                     f"Migrated {executor_result.get('commit_count', 0)} commits")


        # Step 4: Clean History (optional, 80%)
        if migration["clean_history"]:
            update_status(MigrationStatus.CLEANING, "Cleaning repository history", 75,
                         "Removing large files and build artifacts...")

            cleaner_config = {
                "repository": {
                    "path": executor_result.get("repo_path")
                },
                "cleanup": {
                    "patterns": ["*.exe", "*.dll", "*.zip", "*.tar.gz", "node_modules", "bin", "obj"]
                }
            }
            cleaner_result = run_agent("history_cleaner", cleaner_config)
            update_status(MigrationStatus.CLEANING, "History cleaned", 80,
                         f"Cleaned {cleaner_result.get('files_removed', 0)} files")

        # Step 5: Setup GitHub Repository (85%)
        update_status(MigrationStatus.PUSHING, "Setting up GitHub repository", 85,
                     "Creating GitHub repository...")

        github_config = {
            "github": {
                "organization": migration["github_org"],
                "repository": migration["github_repo"],
                "token": migration["github_token"],
                "private": True,
                "description": f"Migrated from {migration['svn_url']}"
            }
        }
        github_result = run_agent("github_setup", github_config)
        update_status(MigrationStatus.PUSHING, "GitHub repository created", 90,
                     f"Repository URL: {github_result.get('repo_url')}")

        # Step 6: Push and Verify (95%)
        update_status(MigrationStatus.PUSHING, "Pushing to GitHub", 90,
                     "Pushing all branches and tags...")

        push_config = {
            "repository": {
                "local_path": executor_result.get("repo_path")
            },
            "github": {
                "url": github_result.get("repo_url"),
                "token": migration["github_token"]
            },
            "verification": {
                "compare_commits": True
            }
        }
        push_result = run_agent("push_verifier", push_config)
        update_status(MigrationStatus.PUSHING, "Push complete", 95,
                     f"Pushed {push_result.get('branches_pushed', 0)} branches, {push_result.get('tags_pushed', 0)} tags")

        # Complete
        update_status(MigrationStatus.COMPLETED, "Migration completed successfully", 100,
                     "Migration finished! Repository available on GitHub")

    except Exception as e:
        migration["status"] = MigrationStatus.FAILED
        migration["error"] = str(e)
        migration["logs"].append(f"{datetime.utcnow().isoformat()}: ERROR: {str(e)}")
        migration["updated_at"] = datetime.utcnow().isoformat()


if __name__ == "__main__":
    import uvicorn
    # Create temp directory
    os.makedirs("D:\\Svn2Github\\temp", exist_ok=True)
    uvicorn.run(app, host="0.0.0.0", port=5000)
