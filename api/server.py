"""
Main FastAPI application server for SVN to GitHub migration system.
"""

from fastapi import FastAPI, HTTPException, Depends, WebSocket, WebSocketDisconnect, Query
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy.orm import Session
from typing import List, Optional
import uvicorn
import os
import sys

# Add parent directory to path to import agents
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from api.database import init_db, get_db
from api import schemas
from api.routes import migrations, logs, authors, config, statistics, verification, audit

# Initialize FastAPI app
app = FastAPI(
    title="SVN to GitHub Migration API",
    description="REST API for automating SVN to GitHub migrations with real-time monitoring",
    version="1.0.0"
)

# CORS middleware - allows WPF app to connect
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, specify exact origins
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include routers
app.include_router(migrations.router, prefix="/api/migrations", tags=["migrations"])
app.include_router(logs.router, prefix="/api/logs", tags=["logs"])
app.include_router(authors.router, prefix="/api/authors", tags=["authors"])
app.include_router(config.router, prefix="/api/config", tags=["config"])
app.include_router(statistics.router, prefix="/api/statistics", tags=["statistics"])
app.include_router(verification.router, prefix="/api/verification", tags=["verification"])
app.include_router(audit.router, prefix="/api/audit", tags=["audit"])


@app.on_event("startup")
async def startup_event():
    """Initialize database on startup."""
    init_db()
    print("Database initialized")
    print("API server starting...")


@app.get("/")
async def root():
    """Root endpoint."""
    return {
        "message": "SVN to GitHub Migration API",
        "version": "1.0.0",
        "docs": "/docs",
        "health": "/health"
    }


@app.get("/health")
async def health_check():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "database": "connected"
    }


@app.get("/api/info")
async def api_info():
    """Get API information."""
    return {
        "name": "SVN to GitHub Migration API",
        "version": "1.0.0",
        "endpoints": {
            "migrations": "/api/migrations",
            "logs": "/api/logs",
            "authors": "/api/authors",
            "config": "/api/config",
            "statistics": "/api/statistics",
            "verification": "/api/verification",
            "audit": "/api/audit"
        }
    }


if __name__ == "__main__":
    # Run the server
    uvicorn.run(
        "server:app",
        host="0.0.0.0",
        port=8000,
        reload=True,
        log_level="info"
    )
