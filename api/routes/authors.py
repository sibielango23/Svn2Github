"""
Author mapping endpoints for the API.
"""

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from typing import List

from api.database import get_db, Author, Migration
from api.schemas import AuthorResponse, AuthorCreate, AuthorBulkUpdate

router = APIRouter()


@router.get("/{migration_id}", response_model=List[AuthorResponse])
async def get_authors(migration_id: str, db: Session = Depends(get_db)):
    """Get all authors for a migration."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    authors = db.query(Author).filter(Author.migration_id == migration_id).all()
    return authors


@router.post("/{migration_id}", response_model=AuthorResponse, status_code=201)
async def create_author(
    migration_id: str,
    author: AuthorCreate,
    db: Session = Depends(get_db)
):
    """Create a new author mapping."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    # Check if author already exists
    existing = db.query(Author).filter(
        Author.migration_id == migration_id,
        Author.svn_username == author.svn_username
    ).first()

    if existing:
        raise HTTPException(status_code=400, detail="Author already exists")

    # Create new author
    new_author = Author(
        migration_id=migration_id,
        svn_username=author.svn_username,
        git_name=author.git_name,
        git_email=author.git_email,
        verified=author.verified
    )

    db.add(new_author)
    db.commit()
    db.refresh(new_author)

    return new_author


@router.put("/{migration_id}/{author_id}", response_model=AuthorResponse)
async def update_author(
    migration_id: str,
    author_id: int,
    author_update: AuthorCreate,
    db: Session = Depends(get_db)
):
    """Update an author mapping."""
    author = db.query(Author).filter(
        Author.id == author_id,
        Author.migration_id == migration_id
    ).first()

    if not author:
        raise HTTPException(status_code=404, detail="Author not found")

    # Update fields
    author.git_name = author_update.git_name
    author.git_email = author_update.git_email
    author.verified = author_update.verified

    db.commit()
    db.refresh(author)

    return author


@router.post("/{migration_id}/bulk", response_model=List[AuthorResponse])
async def bulk_update_authors(
    migration_id: str,
    bulk_update: AuthorBulkUpdate,
    db: Session = Depends(get_db)
):
    """Bulk update author mappings."""
    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    updated_authors = []

    for author_data in bulk_update.authors:
        # Find existing author
        author = db.query(Author).filter(
            Author.migration_id == migration_id,
            Author.svn_username == author_data.svn_username
        ).first()

        if author:
            # Update existing
            author.git_name = author_data.git_name
            author.git_email = author_data.git_email
            author.verified = author_data.verified
        else:
            # Create new
            author = Author(
                migration_id=migration_id,
                svn_username=author_data.svn_username,
                git_name=author_data.git_name,
                git_email=author_data.git_email,
                verified=author_data.verified
            )
            db.add(author)

        updated_authors.append(author)

    db.commit()

    # Refresh all
    for author in updated_authors:
        db.refresh(author)

    return updated_authors


@router.delete("/{migration_id}/{author_id}")
async def delete_author(
    migration_id: str,
    author_id: int,
    db: Session = Depends(get_db)
):
    """Delete an author mapping."""
    author = db.query(Author).filter(
        Author.id == author_id,
        Author.migration_id == migration_id
    ).first()

    if not author:
        raise HTTPException(status_code=404, detail="Author not found")

    db.delete(author)
    db.commit()

    return {"message": "Author deleted"}


@router.post("/{migration_id}/extract", response_model=List[AuthorResponse])
async def extract_authors_from_svn(
    migration_id: str,
    db: Session = Depends(get_db)
):
    """Extract authors from SVN repository."""
    import subprocess
    import re

    # Check if migration exists
    migration = db.query(Migration).filter(Migration.id == migration_id).first()
    if not migration:
        raise HTTPException(status_code=404, detail="Migration not found")

    try:
        # Get SVN URL from migration
        svn_url = migration.svn_url

        # Run svn log --quiet to extract authors
        result = subprocess.run(
            ['svn', 'log', '--quiet', '--limit', '5000', svn_url],
            capture_output=True,
            text=True,
            timeout=300
        )

        # Extract unique authors
        authors_set = set()
        for line in result.stdout.split('\n'):
            match = re.search(r'r\d+ \| (\S+) \|', line)
            if match:
                authors_set.add(match.group(1))

        # Create author entries
        created_authors = []
        for svn_username in sorted(authors_set):
            # Check if already exists
            existing = db.query(Author).filter(
                Author.migration_id == migration_id,
                Author.svn_username == svn_username
            ).first()

            if not existing:
                # Generate default Git name and email
                git_name = _guess_full_name(svn_username)
                git_email = f"{svn_username}@example.com"

                author = Author(
                    migration_id=migration_id,
                    svn_username=svn_username,
                    git_name=git_name,
                    git_email=git_email,
                    verified=False
                )
                db.add(author)
                created_authors.append(author)

        db.commit()

        # Return all authors
        all_authors = db.query(Author).filter(
            Author.migration_id == migration_id
        ).all()

        return all_authors

    except subprocess.TimeoutExpired:
        raise HTTPException(
            status_code=408,
            detail="SVN extraction timed out. Try with a smaller revision range."
        )
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to extract authors: {str(e)}"
        )


def _guess_full_name(username: str) -> str:
    """Convert username to a readable full name."""
    if '.' in username:
        parts = username.split('.')
        return ' '.join(p.capitalize() for p in parts)
    else:
        return username.capitalize()


@router.get("/{migration_id}/export")
async def export_authors(migration_id: str, db: Session = Depends(get_db)):
    """Export authors in authors.txt format for git-svn."""
    authors = db.query(Author).filter(Author.migration_id == migration_id).all()

    if not authors:
        raise HTTPException(status_code=404, detail="No authors found")

    lines = []
    for author in authors:
        git_name = author.git_name or author.svn_username.capitalize()
        git_email = author.git_email or f"{author.svn_username}@example.com"
        lines.append(f"{author.svn_username} = {git_name} <{git_email}>")

    content = "\n".join(lines)
    filename = f"authors_{migration_id}.txt"

    return {"content": content, "filename": filename}
