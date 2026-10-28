# Migration Flow Diagram

## Complete Migration Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│                      Orchestrator                            │
│               (Python orchestration script)                  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Configuration
                         ▼
        ┌────────────────────────────────┐
        │  migration-config.json          │
        │  • SVN URL                      │
        │  • GitHub org/repo              │
        │  • Migration options            │
        └────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│                    Phase 1: Analysis                        │
│                                                              │
│   ┌─────────────────────────────────────────┐              │
│   │   Repository Analyzer Agent             │              │
│   │   • Detect SVN layout (standard/custom) │              │
│   │   • List branches and tags              │              │
│   │   • Count total revisions               │              │
│   │   • Extract author sample               │              │
│   │   • Estimate complexity                 │              │
│   └─────────────────────────────────────────┘              │
│                         │                                    │
│                         ▼                                    │
│                analysis-results.json                         │
└────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│                  Phase 2: Author Mapping                    │
│                                                              │
│   ┌─────────────────────────────────────────┐              │
│   │   Author Mapper Agent                   │              │
│   │   • Extract all unique SVN usernames    │              │
│   │   • Generate full name guesses          │              │
│   │   • Create email addresses              │              │
│   │   • Write authors.txt file              │              │
│   └─────────────────────────────────────────┘              │
│                         │                                    │
│                         ▼                                    │
│                    authors.txt                               │
│         (john.doe = John Doe <john.doe@company.com>)        │
└────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│               Phase 3: Migration Execution                  │
│                  ⚠️  LONGEST PHASE (hours/days)            │
│                                                              │
│   ┌─────────────────────────────────────────┐              │
│   │   Migration Executor Agent              │              │
│   │   • Run git svn clone                   │              │
│   │   • Process each SVN revision           │              │
│   │   • Convert branches to Git branches    │              │
│   │   • Convert tags to Git tags            │              │
│   │   • Map authors using authors.txt       │              │
│   │   • Preserve timestamps                 │              │
│   └─────────────────────────────────────────┘              │
│                         │                                    │
│                    Progress Monitor                          │
│              (r1/100000, r2/100000, ...)                    │
│                         │                                    │
│                         ▼                                    │
│              migration-workspace/migrated-repo/              │
│                  (Full Git repository)                       │
└────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│          Phase 4: History Cleaning (Optional)               │
│                                                              │
│   ┌─────────────────────────────────────────┐              │
│   │   History Cleaner Agent                 │              │
│   │   • Remove large binaries               │              │
│   │   • Delete build artifacts              │              │
│   │   • Filter by file patterns             │              │
│   │   • Rewrite Git history                 │              │
│   └─────────────────────────────────────────┘              │
│                         │                                    │
│                         ▼                                    │
│               Cleaned repository                             │
│            (Smaller size, cleaner history)                   │
└────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│               Phase 5: GitHub Setup                         │
│                                                              │
│   ┌─────────────────────────────────────────┐              │
│   │   GitHub Setup Agent                    │              │
│   │   • Create GitHub repository            │              │
│   │   • Set visibility (private/public)     │              │
│   │   • Configure settings                  │              │
│   │   • Enable issues/wiki                  │              │
│   │   • Add topics                          │              │
│   └─────────────────────────────────────────┘              │
│                         │                                    │
│                         ▼                                    │
│        https://github.com/org/repo                          │
│               (Empty repository)                             │
└────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│            Phase 6: Push & Verification                     │
│                                                              │
│   ┌─────────────────────────────────────────┐              │
│   │   Push Verifier Agent                   │              │
│   │   • Add remote to local repo            │              │
│   │   • Push all branches                   │              │
│   │   • Push all tags                       │              │
│   │   • Verify commit count matches         │              │
│   │   • Check for unpushed commits          │              │
│   │   • Generate verification report        │              │
│   └─────────────────────────────────────────┘              │
│                         │                                    │
│                         ▼                                    │
│        GitHub Repository (Complete)                          │
│     ✓ All commits  ✓ All branches  ✓ All tags              │
└────────────────────────────────────────────────────────────┘
                         │
                         ▼
                ┌─────────────────┐
                │  Final Report    │
                │  • Duration      │
                │  • Statistics    │
                │  • Verification  │
                └─────────────────┘
```

## Agent Communication Pattern

```
┌──────────────┐
│ Orchestrator │
└──────┬───────┘
       │
       │ JSON config via stdin
       ├──────────────────────────────┐
       │                              │
       ▼                              ▼
┌─────────────┐                ┌─────────────┐
│   Agent A   │                │   Agent B   │
│             │                │             │
│ 1. Read     │                │ 1. Read     │
│    stdin    │                │    stdin    │
│             │                │             │
│ 2. Process  │                │ 2. Process  │
│    (Python) │                │    (Python) │
│             │                │             │
│ 3. Write    │                │ 3. Write    │
│    stdout   │                │    stdout   │
│             │                │             │
│ 4. Log to   │                │ 4. Log to   │
│    stderr   │                │    stderr   │
└──────┬──────┘                └──────┬──────┘
       │                              │
       │ JSON results                 │
       └──────────────┬───────────────┘
                      │
                      ▼
              ┌───────────────┐
              │ Orchestrator  │
              │ (processes    │
              │  results)     │
              └───────────────┘
```

## Data Flow

```
SVN Repository
      │
      │ svn log, svn list
      ▼
Analysis Results ──────────┐
      │                    │
      │                    │
      │ svn log --quiet    │
      ▼                    │
Author Mapping ────────────┤
      │                    │
      │                    │
      │ git svn clone      │
      │ with authors       │
      ▼                    │
Git Repository ────────────┤
      │                    │
      │                    │
      │ git filter-repo    │
      │ (optional)         │
      ▼                    │
Cleaned Git Repo ──────────┤
      │                    │
      │                    │
      │ gh repo create     │
      ▼                    │
GitHub Repository ─────────┤
      │                    │
      │                    │
      │ git push           │
      ▼                    │
Pushed Repository ─────────┤
      │                    │
      │                    │
      │ verification       │
      ▼                    ▼
Migration Report ──> Final Status
```

## Parallel Processing Potential

Current implementation is sequential, but these can be parallelized:

```
Phase 1: Analysis
    ├──► Branch listing ─────┐
    ├──► Tag listing ─────────┤──► Combined Results
    └──► Author extraction ───┘

Phase 6: Push
    ├──► Push branches ───┐
    └──► Push tags ────────┤──► Verification
```

## Error Recovery Flow

```
Any Agent
    │
    ├─► Success ──────► Next Agent
    │
    └─► Failure ──┐
                  │
                  ▼
         ┌────────────────┐
         │ Error Handler  │
         │ • Log error    │
         │ • Save state   │
         │ • Report       │
         └────────┬───────┘
                  │
                  ├─► Resume? ──► Restart from last checkpoint
                  │
                  └─► Abort ────► Generate partial report
```

## Time Breakdown (Typical 50k revision repo)

```
Phase 1: Analysis          ████ 5%        (~30 min)
Phase 2: Author Mapping    █ 1%           (~5 min)
Phase 3: Migration         ████████████████████████████ 85% (~10-20 hrs)
Phase 4: History Cleaning  ██ 5%          (~30 min)
Phase 5: GitHub Setup      █ 1%           (~5 min)
Phase 6: Push & Verify     ██ 3%          (~15 min)
                          ─────────────────────────────
                          Total: 12-22 hours
```
