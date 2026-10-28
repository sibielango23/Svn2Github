# SVN to GitHub Migration - Multi-Agent System

## Project Structure

```
svn2github/
├── README.md                    # Project overview
├── MIGRATION_GUIDE.md          # Detailed migration guide
├── orchestrator.py             # Main orchestration script
├── quickstart.sh               # Linux/macOS quick start
├── quickstart.ps1              # Windows quick start
├── migration-config.example.json  # Example configuration
├── agents/                     # Agent implementations
│   ├── __init__.py
│   ├── repository_analyzer.py  # Analyzes SVN structure
│   ├── author_mapper.py        # Maps SVN authors to Git
│   ├── migration_executor.py   # Executes git-svn clone
│   ├── history_cleaner.py      # Cleans repository history
│   ├── github_setup.py         # Creates GitHub repository
│   ├── push_verifier.py        # Pushes and verifies
│   └── agent_template.py       # Template for custom agents
└── migration-workspace/        # Working directory (created)
    ├── analysis-results.json
    ├── authors.txt
    ├── migrated-repo/          # Converted Git repository
    └── migration-report.json
```

## Agent Architecture

### Agent Communication

Agents communicate via:
- **Input**: JSON configuration via stdin
- **Output**: JSON results via stdout
- **Logging**: Progress messages to stderr

### Agent Lifecycle

```
Orchestrator
    ↓
Configuration → Agent (stdin)
    ↓
Agent Processing
    ↓
Results → Orchestrator (stdout)
    ↓
Next Agent
```

### Creating Custom Agents

1. Copy `agents/agent_template.py`
2. Implement your logic in `execute()` method
3. Read config from `json.load(sys.stdin)`
4. Output results with `print(json.dumps(results))`
5. Add to orchestrator pipeline

Example custom agent:

```python
#!/usr/bin/env python3
import json
import sys

class NotificationAgent:
    def __init__(self, config):
        self.config = config
    
    def execute(self):
        # Send notification
        return {
            'success': True,
            'notified': True
        }

def main():
    config = json.load(sys.stdin)
    agent = NotificationAgent(config)
    print(json.dumps(agent.execute()))

if __name__ == '__main__':
    main()
```

## Configuration Schema

```json
{
  "svn": {
    "url": "string (required)",
    "structure": "standard|non-standard",
    "trunk": "string (default: trunk)",
    "branches": "string (default: branches)",
    "tags": "string (default: tags)"
  },
  "github": {
    "org": "string (required)",
    "repo": "string (required)",
    "private": "boolean (default: true)"
  },
  "options": {
    "authors_file": "string (default: authors.txt)",
    "author_domain": "string (default: example.com)",
    "clean_history": "boolean (default: false)",
    "exclude_paths": ["array of glob patterns"],
    "max_file_size_mb": "number (default: 100)"
  },
  "workspace": "string (default: ./migration-workspace)"
}
```

## Running Individual Agents

### 1. Repository Analyzer
```bash
cat config.json | python agents/repository_analyzer.py
```
Output: SVN structure, branches, tags, authors

### 2. Author Mapper
```bash
cat config.json | python agents/author_mapper.py
```
Output: authors.txt file with SVN → Git mappings

### 3. Migration Executor
```bash
cat config.json | python agents/migration_executor.py
```
Output: Git repository in workspace

### 4. History Cleaner
```bash
cat config.json | python agents/history_cleaner.py
```
Output: Cleaned repository (rewrites history)

### 5. GitHub Setup
```bash
cat config.json | python agents/github_setup.py
```
Output: GitHub repository created

### 6. Push Verifier
```bash
cat config.json | python agents/push_verifier.py
```
Output: Verification results

## Advanced Usage

### Parallel Agent Execution

While the orchestrator runs agents sequentially, you can parallelize independent agents:

```bash
# Run analysis and GitHub setup in parallel
cat config.json | python agents/repository_analyzer.py &
cat config.json | python agents/github_setup.py &
wait
```

### Incremental Migration

For very large repositories, migrate incrementally:

```bash
# Migrate last 1000 revisions only
git svn clone -r 99000:HEAD <url>
```

### Custom Pipeline

Create your own orchestration:

```python
from pathlib import Path
import subprocess
import json

def run_custom_pipeline():
    config = json.load(open('config.json'))
    
    # Custom agent sequence
    agents = [
        'repository_analyzer',
        'my_custom_agent',
        'migration_executor',
        'github_setup'
    ]
    
    for agent in agents:
        result = subprocess.run(
            ['python', f'agents/{agent}.py'],
            input=json.dumps(config),
            capture_output=True,
            text=True
        )
        print(f"{agent}: {result.stdout}")
```

## Monitoring and Debugging

### Check Agent Status

```bash
# Watch migration progress
tail -f migration.log

# Monitor disk usage
du -sh migration-workspace/

# Count migrated commits
cd migration-workspace/migrated-repo
git log --oneline | wc -l
```

### Debug Agent Failures

```bash
# Run agent with verbose logging
cat config.json | python -u agents/migration_executor.py 2>&1 | tee debug.log

# Check agent output
cat debug.log | grep ERROR
```

### Resume Failed Migration

```bash
# Resume git-svn fetch
cd migration-workspace/migrated-repo
git svn fetch

# Or restart from specific agent
cat config.json | python agents/push_verifier.py
```

## Performance Optimization

### Large Repository (>100k commits)

1. **Use fast disk**: SSD recommended
2. **Increase memory**: 16GB+ RAM
3. **Shallow clone**: Use revision ranges
4. **Filter paths**: Migrate specific directories only

### Network Optimization

For slow SVN connections:
- Run migration on server close to SVN
- Use `--authors-prog` instead of `--authors-file`
- Enable HTTP compression

## Security Considerations

1. **Credentials**: Use SSH keys, not passwords
2. **Private repos**: Set `"private": true` in config
3. **Sensitive data**: Review history before pushing
4. **Author emails**: Verify author mappings

## Integration with CI/CD

Add post-migration setup:

```bash
# After migration, set up GitHub Actions
gh workflow run setup-ci.yml

# Configure branch protection
gh api repos/{org}/{repo}/branches/main/protection \
  --method PUT \
  --input protection.json
```

## Troubleshooting

See [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md#troubleshooting) for common issues and solutions.

## Contributing

To add a new agent:

1. Create `agents/your_agent.py` using template
2. Update orchestrator.py to include it
3. Add documentation
4. Test with example repository

## License

MIT License - see LICENSE file
