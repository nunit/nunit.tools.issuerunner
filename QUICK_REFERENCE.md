# IssueRunner Quick Reference

## Build
```bash
cd Tools
dotnet build -c Release
```

## Common Commands

### Metadata Management
```bash
# Sync from GitHub
./IssueRunner.Cli metadata sync-from-github --root /repo/path

# Update issue folders
./IssueRunner.Cli metadata sync-to-folders --root /repo/path
```

### Running Tests
```bash
# All regression tests
./IssueRunner.Cli run --scope Regression

# All open issues
./IssueRunner.Cli run --scope Open

# Specific issues
./IssueRunner.Cli run --issues 228,343,1015

# Linux - skip .NET Framework
./IssueRunner.Cli run --skip-netfx

# Windows - only .NET Framework
./IssueRunner.Cli run --only-netfx

# Fast mode - NUnit packages only
./IssueRunner.Cli run --nunit-only

# Custom scripts only
./IssueRunner.Cli run --test-types Script
```

### Reporting
```bash
# Generate report
./IssueRunner.Cli report generate

# Check regressions (CI gate)
./IssueRunner.Cli report check-regressions
```

### Multi-OS Merge
```bash
./IssueRunner.Cli merge --linux ./linux-artifacts --windows ./windows-artifacts
```

## Python to C# Command Map

| Python | C# |
|--------|-----|
| `python scripts/update_central_from_github.py` | `./IssueRunner.Cli metadata sync-from-github` |
| `python scripts/update_folders_from_central.py` | `./IssueRunner.Cli metadata sync-to-folders` |
| `python scripts/run_tests.py --scope regression-only` | `./IssueRunner.Cli run --scope Regression` |
| `python scripts/run_tests.py --skip-netfx` | `./IssueRunner.Cli run --skip-netfx` |
| `python scripts/testreport.py` | `./IssueRunner.Cli report generate` |
| `python scripts/check_regressions.py` | `./IssueRunner.Cli report check-regressions` |
| `python scripts/merge_results.py --linux A --windows B` | `./IssueRunner.Cli merge --linux A --windows B` |

## Environment Variables

- **GITHUB_TOKEN** - GitHub API token for higher rate limits

```bash
export GITHUB_TOKEN=ghp_yourtoken
```

## Output Files

- **results.json** - Aggregated test results
- **TestReport.md** - Markdown report
- **TestResults-consolelog.md** - Console output log
- **scripts/issues_metadata.json** - Central metadata (GitHub)
- **Issue*/issue_metadata.json** - Per-issue metadata

## Marker Files (Skip Issues)

Create in issue folder to skip:
- `ignore` or `ignore.md` - Skip completely
- `explicit` or `explicit.md` - Explicit run only
- `wip` or `wip.md` - Work in progress

## Performance Tips

1. Use `--nunit-only` for faster package updates (~80% speedup)
2. Use `--test-types DotNet` to skip custom script issues
3. Use `--issues` to test specific issues during development
4. Split by platform (`--skip-netfx` / `--only-netfx`) for parallel CI

## Troubleshooting

### Build fails
```bash
dotnet --version  # Ensure .NET 10 SDK installed
```

### GitHub API rate limit
```bash
export GITHUB_TOKEN=ghp_yourtoken
```

### Timeout errors
```bash
./IssueRunner.Cli run --timeout 1200  # Increase to 20 minutes
```

### Tests not found
Check for marker files (ignore/explicit/wip) in issue folders

## CI Workflows

- **regression-tests-dotnet.yml** - .NET Core regressions (Linux)
- **regression-tests-netfx.yml** - .NET Framework regressions (Windows)
- **open-issues-dotnet.yml** - Open issues .NET Core (weekly)
- **open-issues-netfx.yml** - Open issues .NET Framework (weekly)
- **aggregate-report.yml** - Merge and report

## Getting Help

```bash
./IssueRunner.Cli --help
./IssueRunner.Cli run --help
./IssueRunner.Cli metadata --help
./IssueRunner.Cli report --help
```

## Documentation

- **README.md** - Complete guide
- **IMPLEMENTATION_SUMMARY.md** - Migration details
- **FILES_CREATED.md** - File inventory
