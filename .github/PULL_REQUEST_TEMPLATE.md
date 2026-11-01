# Pull Request

## 📝 Description

Brief description of what this PR does.

Fixes # (issue number)

## 🎯 Type of Change

- [ ] 🐛 Bug fix (non-breaking change which fixes an issue)
- [ ] ✨ New feature (non-breaking change which adds functionality)
- [ ] 💥 Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] 📚 Documentation update
- [ ] 🔧 Configuration change
- [ ] ♻️ Code refactoring
- [ ] ⚡ Performance improvement
- [ ] ✅ Test addition/improvement

## 🔄 Changes Made

Detailed list of changes:

- Change 1
- Change 2
- Change 3

## 🧪 Testing

Describe how you tested these changes:

- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed
- [ ] WPF UI tested
- [ ] API endpoints tested
- [ ] Migration workflow functionality tested with disposable repositories
- [ ] Checkpoint/retry and credential lifecycle tested
- [ ] Portable package built and smoke-tested when applicable
- [ ] Local readiness, report redaction, and explicit copilot sharing tested when applicable

**Test Configuration:**
- OS: 
- .NET Version:
- Deployment (source/portable):
- Git/git-svn, SVN, gh versions:
- Optional git-filter-repo/Python version (only when testing cleanup):

Record each exact validation command and its result (passed/failed/not run).
Distinguish local UI/state checks from real migrations and portable release
acceptance; do not infer enterprise readiness from a passing build.

## 📸 Screenshots (if applicable)

Add screenshots showing the changes.

## ✅ Checklist

- [ ] My code follows the project's code style (see `.github/copilot-instructions.md`)
- [ ] I have performed a self-review of my code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have updated the documentation accordingly
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes
- [ ] Any dependent changes have been merged and published

## 📚 Documentation Updated

- [ ] README.md
- [ ] MIGRATION_GUIDE.md
- [ ] API documentation
- [ ] Portable package guide and generated package README
- [ ] WPF test plan and current validation results
- [ ] Migration copilot guide
- [ ] Code comments
- [ ] None needed

## 🔗 Related PRs/Issues

List any related pull requests or issues:

- Related to #
- Depends on #
- Blocks #

## 📊 Performance Impact

Does this change affect performance?

- [ ] No performance impact
- [ ] Performance improved
- [ ] Performance degraded (explain why this is acceptable)

## 🔐 Security Considerations

Does this PR introduce any security concerns?

- [ ] No security impact
- [ ] Security improved
- [ ] Potential security concerns (describe and justify)

## 🚀 Deployment Notes

Any special deployment considerations?
State whether portable binaries were rebuilt from this source. Existing ZIPs
do not update automatically. Note checkpoint/workspace compatibility and
whether database/workspace backups are needed. Do not include credentials.

## 💬 Additional Notes

Any additional information for reviewers.

---

**Reviewer Checklist:**
- [ ] Code quality is good
- [ ] Tests are sufficient
- [ ] Documentation is complete
- [ ] No security issues
- [ ] Breaking changes are acceptable
