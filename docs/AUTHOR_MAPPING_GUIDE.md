# Author Mapping in SVN to GitHub Migration

## Overview

Author mapping is a critical step in migrating from SVN to Git/GitHub. Since SVN stores authors as simple usernames (e.g., `jdoe`) while Git requires full name and email format (e.g., `John Doe <john.doe@company.com>`), we need to map each SVN username to a proper Git identity.

## How It Works

### 1. **Automatic Extraction**
When you click "Load From SVN" in the Author Mapping UI, the system:

```python
# Agent extracts authors from SVN log
svn log --quiet --limit 5000 https://your-svn-repo

# Output is parsed for unique usernames
r1 | jdoe | 2010-01-15 ...
r2 | asmith | 2010-01-16 ...
r3 | jdoe | 2010-01-17 ...

# Result: ['asmith', 'jdoe']
```

### 2. **Auto-Generation of Git Identities**
The system makes educated guesses for Git format:

| SVN Username | Generated Git Name | Generated Git Email |
|--------------|-------------------|-------------------|
| `jdoe` | John Doe | jdoe@example.com |
| `john.doe` | John Doe | john.doe@example.com |
| `asmith` | Asmith | asmith@example.com |

**Algorithm:**
```python
def guess_full_name(username):
    if '.' in username:
        # firstname.lastname → Firstname Lastname
        parts = username.split('.')
        return ' '.join(p.capitalize() for p in parts)
    else:
        # single word → Capitalize
        return username.capitalize()

email = f"{username}@example.com"
```

### 3. **Interactive Review in WPF UI**

#### **Author Mapping Window Features:**

**Toolbar:**
- 🔄 **Load From SVN** - Extract all authors from repository
- ➕ **Add Author** - Manually add missing author
- 🔍 **Search** - Filter authors by any field
- 📊 **Stats** - Shows total and verified count

**Editable Grid:**
| SVN Username | Git Name ✏️ | Git Email ✏️ | Verified ✅ | Actions |
|--------------|------------|-------------|-------------|---------|
| jdoe | John Doe | john.doe@company.com | ✓ | 🗑️ |
| asmith | Alice Smith | alice.smith@company.com | ✓ | 🗑️ |

- **Read-only**: SVN Username (source of truth)
- **Editable**: Git Name, Git Email (double-click to edit)
- **Checkbox**: Verified (mark when confirmed correct)
- **Delete**: Remove incorrect mappings

**Bottom Actions:**
- 💾 **Export to File** - Save as `authors.txt` for external use
- ✅ **Save & Close** - Save all changes to database

### 4. **Database Storage**

All author mappings are stored in SQLite database:

```sql
CREATE TABLE authors (
    id INTEGER PRIMARY KEY,
    migration_id TEXT NOT NULL,
    svn_username TEXT NOT NULL,
    git_name TEXT NOT NULL,
    git_email TEXT NOT NULL,
    verified BOOLEAN DEFAULT 0,
    UNIQUE(migration_id, svn_username)
);
```

### 5. **Git-SVN Authors File Generation**

When migration runs, authors are exported to `authors.txt`:

```
jdoe = John Doe <john.doe@company.com>
asmith = Alice Smith <alice.smith@company.com>
bwilson = Bob Wilson <bob.wilson@company.com>
```

This file is used by `git-svn` during migration:

```bash
git svn clone https://svn-repo \
    --authors-file=authors.txt \
    --stdlayout
```

## Workflow in the WPF App

### **Scenario 1: Configure Before Migration**

```
1. User fills SVN URL in "New Migration" form
2. User clicks "Configure" in Author Mapping section
3. Author Mapping window opens
4. User clicks "Load From SVN"
   ↓
   System extracts all authors (may take 2-5 minutes for large repos)
   ↓
   Grid shows all authors with auto-generated names/emails
5. User reviews and edits:
   - Double-click Git Name cell → Edit
   - Double-click Git Email cell → Edit
   - Check "Verified" when correct
6. User clicks "Save & Close"
   ↓
   Authors saved to database
7. User clicks "START MIGRATION"
   ↓
   Migration uses saved author mappings
```

### **Scenario 2: Configure During Migration**

```
1. Migration is running (status: "MappingAuthors")
2. User clicks "Edit Authors" button in migration detail view
3. Author Mapping window opens with current mappings
4. User edits incorrect mappings
5. User clicks "Save & Close"
   ↓
   Authors updated in real-time
   ↓
   Migration continues with corrected mappings
```

### **Scenario 3: Post-Migration Fix**

```
1. Migration completed but some authors look wrong
2. User opens migration details
3. User clicks "Re-map Authors"
4. User corrects mappings
5. User clicks "Re-run Migration" (optional)
   ↓
   Migration re-runs with corrected author history
```

## API Endpoints Used

### **GET** `/api/authors/{migration_id}`
Returns existing author mappings for a migration.

```json
[
  {
    "id": 1,
    "svn_username": "jdoe",
    "git_name": "John Doe",
    "git_email": "john.doe@company.com",
    "verified": true
  }
]
```

### **POST** `/api/authors/{migration_id}/extract`
Extracts authors from SVN repository.

**Process:**
1. Reads SVN URL from migration record
2. Runs `svn log --quiet --limit 5000`
3. Parses usernames with regex: `r\d+ \| (\S+) \|`
4. Generates default Git identities
5. Creates author records in database
6. Returns all authors

### **POST** `/api/authors/{migration_id}/bulk`
Bulk update all authors at once.

```json
{
  "authors": [
    {
      "svn_username": "jdoe",
      "git_name": "John Doe",
      "git_email": "john.doe@company.com",
      "verified": true
    }
  ]
}
```

### **GET** `/api/authors/{migration_id}/export`
Exports authors in git-svn format.

```json
{
  "content": "jdoe = John Doe <john.doe@company.com>\nasmith = Alice Smith <alice.smith@company.com>",
  "filename": "authors_abc123.txt"
}
```

## Behind the Scenes: Author Mapper Agent

The Python agent (`author_mapper.py`) does the heavy lifting:

```python
class AuthorMapper:
    def create_mapping(self) -> Dict[str, Any]:
        # Extract all unique SVN authors
        authors = self._extract_all_authors()
        
        # Generate authors.txt file
        authors_file = self._generate_authors_file(authors)
        
        return {
            'authors': authors,
            'authors_count': len(authors),
            'file': str(authors_file)
        }
    
    def _extract_all_authors(self) -> List[str]:
        """Extract from full 15-year history."""
        result = subprocess.run(
            ['svn', 'log', '--quiet', self.svn_url],
            capture_output=True,
            timeout=600  # 10 minute timeout
        )
        
        authors = set()
        for line in result.stdout.split('\n'):
            match = re.search(r'r\d+ \| (\S+) \|', line)
            if match:
                authors.add(match.group(1))
        
        return sorted(list(authors))
```

## Best Practices

### ✅ **Do:**
1. **Review all auto-generated mappings** - The algorithm makes educated guesses but isn't perfect
2. **Use real email addresses** - Helps with attribution in GitHub
3. **Check "Verified"** - Mark each author as verified after confirming correctness
4. **Extract early** - Load authors before starting migration to catch issues
5. **Save frequently** - Changes auto-save but clicking "Save & Close" ensures persistence

### ❌ **Don't:**
1. **Don't skip author mapping** - Git requires proper format; skipping causes errors
2. **Don't use fake emails carelessly** - `@example.com` works but real emails are better
3. **Don't delete active authors** - If someone has commits, they need a mapping
4. **Don't forget to verify** - Unverified mappings may cause issues later

## Common Issues & Solutions

### **Issue 1: "Timeout while extracting authors"**
**Cause:** Repository is very large (15+ years)  
**Solution:**
```python
# Agent falls back to recent history
svn log --quiet --limit 5000  # Last 5000 commits
```
Then manually add any old authors you know about.

### **Issue 2: "Author 'unknown' not found"**
**Cause:** SVN commit has no author or system account  
**Solution:** Add manually:
- SVN Username: `unknown`
- Git Name: `Unknown Author`
- Git Email: `unknown@legacy-system.com`

### **Issue 3: "Duplicate email addresses"**
**Cause:** Multiple SVN users share same email  
**Solution:** Git allows this! Just ensure names are different:
- `jdoe = John Doe <john@company.com>`
- `jane.doe = Jane Doe <john@company.com>` ← Different name, same email

### **Issue 4: "Special characters in username"**
**Cause:** SVN allows `first.last`, `name_123`, etc.  
**Solution:** These work fine in Git:
- `first.last = First Last <first.last@company.com>`
- `name_123 = Name 123 <name123@company.com>`

## Architecture Diagram

```
┌─────────────────┐
│   WPF UI        │
│  Author         │
│  Mapping        │
│  Window         │
└────────┬────────┘
         │
         │ HTTP API Calls
         ↓
┌─────────────────┐
│   FastAPI       │
│   /api/authors  │
│   - GET         │
│   - POST extract│
│   - POST bulk   │
│   - GET export  │
└────────┬────────┘
         │
         ↓
┌─────────────────┐     ┌─────────────────┐
│  SQLite DB      │────→│  authors.txt    │
│  authors table  │     │  (git-svn       │
│                 │     │   format)       │
└─────────────────┘     └─────────────────┘
         ↑                       │
         │                       │
         │                       ↓
┌─────────────────┐     ┌─────────────────┐
│  Author Mapper  │     │   git-svn       │
│  Agent (Python) │     │   clone         │
│  - Extract      │     │   --authors-file│
│  - Generate     │     └─────────────────┘
└─────────────────┘
         ↑
         │
         │ svn log --quiet
         ↓
┌─────────────────┐
│   SVN Repo      │
│   (15 years)    │
└─────────────────┘
```

## Summary

The **Author Mapper** is fully integrated into your WPF application:

1. ✅ **Extract** - Automatically pulls all SVN authors
2. ✅ **Generate** - Creates intelligent Git format guesses
3. ✅ **Review** - Interactive UI to edit and verify
4. ✅ **Store** - Persistent database storage
5. ✅ **Export** - Git-svn compatible output
6. ✅ **Integrate** - Seamless migration workflow

**You get complete control over author mapping with zero command-line knowledge required!**
