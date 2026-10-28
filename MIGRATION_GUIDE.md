# SVN to GitHub Migration Guide

Complete guide for migrating a 15-year SVN repository to GitHub using the multi-agent system.

## Prerequisites

### Required Tools

1. **Git with git-svn**
   ```bash
   # Windows (Git for Windows includes git-svn)
   winget install Git.Git
   
   # Linux
   sudo apt-get install git git-svn
   
   # macOS
   brew install git
   ```

2. **SVN Client**
   ```bash
   # Windows
   winget install TortoiseSVN.TortoiseSVN
   
   # Linux
   sudo apt-get install subversion
   
   # macOS
   brew install subversion
   ```

3. **GitHub CLI**
   ```bash
   # Windows
   winget install GitHub.cli
   
   # Linux/macOS
   brew install gh
   
   # Then authenticate
   gh auth login
   ```

4. **Python 3.8+**
   ```bash
   python --version  # Should be 3.8 or higher
   ```

5. **git-filter-repo** (optional, for history cleaning)
   ```bash
   pip install git-filter-repo
   ```

## Migration Process

### 1. Prepare Configuration

Copy the example configuration:
```bash
cp migration-config.example.json migration-config.json
```

Edit `migration-config.json`:

```json
{
  "svn": {
    "url": "https://svn.yourcompany.com/your-repo",
    "structure": "standard"
  },
  "github": {
    "org": "your-github-org",
    "repo": "migrated-repo",
    "private": true
  },
  "options": {
    "author_domain": "yourcompany.com",
    "clean_history": false
  }
}
```

### 2. Test SVN Access

Verify you can access the SVN repository:

```bash
svn info https://svn.yourcompany.com/your-repo
```

### 3. Run Migration

#### Option A: Full Automated Migration

```bash
python orchestrator.py --config migration-config.json
```

This runs all six agents in sequence:
1. Repository Analyzer
2. Author Mapper
3. Migration Executor
4. History Cleaner (if enabled)
5. GitHub Setup
6. Push & Verification

#### Option B: Step-by-Step Migration

Run each agent individually for more control:

```bash
# 1. Analyze repository
cat migration-config.json | python agents/repository_analyzer.py > analysis.json

# 2. Map authors
cat migration-config.json | python agents/author_mapper.py > authors.json

# 3. Execute migration (this is the long-running step)
cat migration-config.json | python agents/migration_executor.py > migration.json

# 4. Clean history (optional)
cat migration-config.json | python agents/history_cleaner.py > cleaned.json

# 5. Create GitHub repository
cat migration-config.json | python agents/github_setup.py > github.json

# 6. Push and verify
cat migration-config.json | python agents/push_verifier.py > verify.json
```

### 4. Monitor Progress

For a 15-year repository, the migration can take **several hours to days**. The git-svn clone step is the longest.

Monitor progress:
- Watch console output for revision numbers
- Check `migration-workspace/migrated-repo` directory
- Monitor disk space (large repos can be 10GB+)

## Time Estimates

Based on repository size:

| Revisions | Estimated Time | Disk Space |
|-----------|---------------|------------|
| < 10,000  | 1-4 hours     | 1-5 GB     |
| 10,000-50,000 | 4-12 hours | 5-20 GB    |
| 50,000-100,000 | 12-24 hours | 20-50 GB   |
| > 100,000 | 1-3 days      | 50+ GB     |

For 15 years of active development, expect **50,000-100,000+ revisions**.

## Resuming Failed Migrations

If migration fails midway:

1. The git-svn clone creates a partial repository
2. Resume with:
   ```bash
   cd migration-workspace/migrated-repo
   git svn fetch
   ```

3. Or re-run the orchestrator (it will skip completed steps)

## Author Mapping

### Interactive UI Method (Recommended)

The WPF application provides a complete visual interface for author mapping:

1. **Open Author Mapping Window**
   - From New Migration form, click **"Configure"** in Author Mapping section
   
2. **Extract Authors from SVN**
   - Click **"Load From SVN"** button
   - System extracts all unique authors from repository history
   - Takes 2-5 minutes (10 minutes for very large repos)

3. **Review Auto-Generated Mappings**
   
   The system intelligently converts SVN usernames:
   
   | SVN Username | Generated Git Name | Generated Email |
   |--------------|-------------------|-----------------|
   | `jdoe` | Jdoe | jdoe@example.com |
   | `john.doe` | John Doe | john.doe@example.com |
   | `alice.m.smith` | Alice M Smith | alice.m.smith@example.com |

4. **Edit Mappings**
   - Double-click any Git Name or Git Email cell to edit
   - Use search box to filter authors
   - Add missing authors manually with "Add Author" button
   - Delete incorrect entries with delete icon
   - Check "Verified" box when mapping is correct

5. **Save Changes**
   - Click **"Save & Close"** to persist all mappings to database
   - Or click **"Export to File"** to save as authors.txt

### Manual Method (Command Line)

Edit the `authors.txt` file directly before migration:

```bash
# Edit authors file
nano migration-workspace/authors.txt
```

Format:
```
john.doe = John Doe <john.doe@company.com>
jane.smith = Jane Smith <jane.smith@company.com>
```

**Important**: Review and edit this file before migration to ensure correct Git attribution.

See **[AUTHOR_MAPPING_GUIDE.md](docs/AUTHOR_MAPPING_GUIDE.md)** for complete documentation.

## History Cleaning

To remove unwanted files from history:

1. Enable in config:
   ```json
   "options": {
     "clean_history": true,
     "exclude_paths": [
       "*.dll",
       "*.exe",
       "build/*"
     ],
     "max_file_size_mb": 100
   }
   ```

2. This removes files matching patterns from **all commits**
3. **Warning**: This rewrites Git history, changing commit SHAs

## Troubleshooting

### "git-svn not found"

Install git-svn:
```bash
# Ubuntu/Debian
sudo apt-get install git-svn

# CentOS/RHEL
sudo yum install git-svn

# macOS - should be included with git
```

### "Permission denied" accessing SVN

- Check SVN credentials
- Try manual SVN checkout first: `svn checkout <url>`
- For HTTPS with certificates, use `--trust-server-cert`

### "Out of memory" during migration

For huge repositories:
1. Increase swap space
2. Use a machine with more RAM (16GB+ recommended)
3. Consider incremental migration (by date ranges)

### Migration takes too long

Options:
1. **Shallow clone**: Start from specific revision
   ```bash
   git svn clone -r 50000:HEAD <url>
   ```

2. **Filter by path**: Migrate only specific directories
   ```bash
   git svn clone --trunk=trunk/project-a <url>
   ```

### Commits missing or incorrect

Check:
- Authors file is complete
- SVN URL includes full repository path
- Standard layout detection is correct

## Post-Migration Steps

After successful migration:

1. **Verify branch protection**
   ```bash
   gh repo edit <org>/<repo> --enable-branch-protection-for-main
   ```

2. **Add collaborators**
   ```bash
   gh repo add-collaborator <org>/<repo> <username>
   ```

3. **Archive SVN repository** (don't delete immediately)

4. **Update CI/CD pipelines** to use GitHub

5. **Notify team** of new repository location

## Advanced: Custom Agent Development

The agent system is modular. Create custom agents by:

1. Create `agents/your_agent.py`
2. Implement config reading from stdin
3. Output results as JSON
4. Add to orchestrator pipeline

Example skeleton:

```python
#!/usr/bin/env python3
import json
import sys

def main():
    config = json.load(sys.stdin)
    
    # Your logic here
    results = {
        'success': True,
        'data': {}
    }
    
    print(json.dumps(results))

if __name__ == '__main__':
    main()
```

## Support Resources

- [git-svn documentation](https://git-scm.com/docs/git-svn)
- [GitHub CLI manual](https://cli.github.com/manual/)
- [git-filter-repo guide](https://github.com/newren/git-filter-repo)

## FAQ

**Q: Will commit history be preserved?**
A: Yes, all commit messages, dates, and authors are preserved.

**Q: What about branches and tags?**
A: All SVN branches and tags are converted to Git branches and tags.

**Q: Can I run this multiple times?**
A: Yes, re-running updates the GitHub repository with new commits.

**Q: Will file permissions be preserved?**
A: Git stores limited permission info. SVN properties may not transfer.

**Q: What about svn:externals?**
A: git-svn doesn't handle externals. Convert to Git submodules manually.
