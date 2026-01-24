# NUnit Issue Runner

Tool for running the repros of issues found in any NUnit issues repository (e.g., NUnit framework issues, NUnit3-VS-Adapter issues).
This repository contains the IssueRunner tool itself, which can be used to manage, test, and track issue reproductions in any compatible issues repository.

## Quick Start

### Building the Tools

```bash
dotnet build -c Release
```

### Using the GUI (Recommended for Interactive Use)

Launch the GUI application for a visual interface:

```bash
# Windows
gui.cmd

# Linux/macOS
./gui.sh
```

### Using the CLI (Recommended for Automation)

Run tests from the command line:

```bash
# Windows
Tools\run-tests.cmd --issues 228,343

# Linux/macOS
./Tools/run-tests.sh --issues 228,343
```

## GUI Application

The IssueRunner GUI provides a desktop application for managing and testing NUnit Adapter issue reproductions. It offers all the functionality of the CLI with a user-friendly visual interface.


### Repository Selection

When the GUI starts, it needs to know which repository to work with:

- **Auto-detection**: If launched from a valid issue repository, it automatically loads that repository
- **File Picker**: Click "Browse" to select a repository folder
- **Persistent Storage**: Your selected repository path is saved to AppData for quick access on subsequent launches
- **Repository Status**: The sidebar shows summary information including test pass/fail counts and NUnit package versions

### Issue List View

The main interface displays all discovered issues from the repository with powerful filtering capabilities.

#### Filtering Options

Filter issues using multiple criteria:

- **Test Scope**: All, Regression (closed issues), Open
- **Issue State**: New, Synced, Failed restore, Failed compile, Runnable, Skipped
- **Test Result**: Success, Fail, Not Tested
- **Test Types**: Scripts only (custom runner scripts), dotnet test only (direct execution)
- **Framework**: .NET, .NET Framework
- **Milestone**: Filter by GitHub milestone
- **Type Labels**: Custom type labels from issue metadata

#### Additional Controls

- **Diff View Toggle**: Show only issues that have changed since baseline
- **Issue Number Entry**: Specify issue numbers directly via text input (comma/space/semicolon separated)
- **Quick Actions**: Inline buttons for running tests, resetting packages, and configuring options

### Running Tests

Click "Run Tests" to execute tests for selected issues.

#### Run Dialog Options

- **Issue Scope**: Select which issues to test
- **Skip .NET Framework**: Skip .NET Framework tests (useful on Linux)
- **.NET Framework Only**: Run only .NET Framework tests
- **Custom Environment Variables**: Set environment variables for test execution
- **Set as Baseline**: Mark current results as the baseline for future comparisons

#### Progress Tracking

During test execution:

- **Real-time Status**: Shows current issue being tested
- **Progress Percentage**: Visual progress indicator
- **Test Counts**: Running totals of passed, failed, skipped tests
- **Cancellation**: Cancel running tests at any time

#### Result Aggregation

After tests complete, results are categorized:

- **Passed**: Tests that succeeded
- **Failed**: Tests that failed
- **Skipped**: Tests that were skipped
- **Not Restored**: Projects that failed NuGet restore
- **Not Compiling**: Projects that failed to build

### Test Status Dashboard

Switch to the Test Status view to see baseline comparisons and detailed results.

#### Baseline Comparison

- **New Passes**: Tests that now pass (were failing or not tested before)
- **New Fails**: Tests that now fail (regressions - were passing before)
- **Fixed Issues**: Tests that previously failed but now pass

#### Per-Issue Breakdown

View detailed test results for each issue, including:

- Individual test outcomes
- Comparison with baseline state
- Baseline date and history

### GitHub Synchronization

Keep issue metadata up-to-date with GitHub.

#### Sync from GitHub Dialog

The sync process has multiple steps:

1. **Download Metadata**: Fetches latest issue data from GitHub API
2. **Create/Update Folders**: Creates issue folders for new issues
3. **Distribute Metadata**: Updates `issue_metadata.json` in each issue folder

#### Metadata Validation

The sync process detects and reports:

- Folders without corresponding metadata entries
- Metadata entries without corresponding folders
- Duplicate metadata entries

Click "Sync from GitHub" in the sidebar or top navbar to start synchronization.

### Package Management

#### Reset Packages

Reset NUnit packages to their baseline versions for selected issues:

1. Select issues in the list (or use filters)
2. Click "Reset Packages"
3. Confirm the reset operation

This restores:

- **TargetFramework(s)** to original values
- **Package versions** to original values
- Converts between singular/plural TargetFramework as needed

#### Set Baseline

Mark current test results as the new baseline:

1. Run tests to get current results
2. In the Run dialog or Test Status view, click "Set as Baseline"
3. Future comparisons will use this baseline

### Reporting and Diagnostics

#### Generate Report

Create a comprehensive test report:

1. Run tests first to generate results
2. Click "Generate Report" in the sidebar
3. Report is saved as `TestReport.md` in the repository root

#### Check Regressions

Identify test failures compared to baseline:

1. Click "Check Regressions" in the sidebar
2. View regression report showing tests that now fail

#### Output Log

The bottom panel shows a real-time output log:

- All operations are logged with timestamps
- Scroll through history to see past operations
- Clear log button to reset the view

## CLI Usage

### Wrapper Scripts

For convenience, use the wrapper scripts that handle paths automatically:

**Windows:**

```cmd
cd /path/to/your/test/repository
..\nunit3-vs-adapter.issues\Tools\run-tests.cmd [options]
..\nunit3-vs-adapter.issues\Tools\generate-report.cmd
..\nunit3-vs-adapter.issues\Tools\sync-from-github.cmd
..\nunit3-vs-adapter.issues\Tools\sync-to-folders.cmd
```

**Linux/macOS:**

```bash
cd /path/to/your/test/repository
../nunit3-vs-adapter.issues/Tools/run-tests.sh [options]
../nunit3-vs-adapter.issues/Tools/generate-report.sh
../nunit3-vs-adapter.issues/Tools/sync-from-github.sh
../nunit3-vs-adapter.issues/Tools/sync-to-folders.sh
```

### Command Structure

```text
issuerunner
├── metadata
│   ├── sync-from-github    Sync metadata from GitHub to central file
│   └── sync-to-folders     Sync metadata from central file to issue folders
├── run                      Run tests for issues
├── reset                    Reset package versions to metadata values
├── report
│   ├── generate            Generate test report
│   └── check-regressions   Check for regression failures
└── merge                    Merge multiple results files
```

### Direct Usage

Run IssueRunner directly:

```bash
cd Tools/IssueRunner/bin/Release/net10.0
./IssueRunner run [options]
```

### Basic Examples

```bash
# Run all tests
./IssueRunner run

# Run specific issues
./IssueRunner run --issues 228,343,1015

# Run all regression tests (closed issues)
./IssueRunner run --scope RegressionOnly

# Run only open issues
./IssueRunner run --scope OpenOnly

# Skip .NET Framework tests (useful on Linux)
./IssueRunner run --skip-netfx
```

## CLI Reference

### Run Command Options

| Option | Description |
| ------ | ----------- |
| `--root <path>` | Repository root path (default: current directory, or ISSUERUNNER_ROOT env var) |
| `--scope <scope>` | Test scope (default: All) |
| `--issues <numbers>` | Comma-separated issue numbers to run |
| `--timeout <seconds>` | Timeout per command (default: 600) |
| `--skip-netfx` | Skip .NET Framework tests |
| `--only-netfx` | Run only .NET Framework tests |
| `--nunit-only` | Update only NUnit packages (faster) |
| `--execution-mode <mode>` | Filter by execution method (default: All) |
| `--feed <feed>` | Package feed (default: Stable) |
| `--verbosity <level>` | Logging verbosity (Normal or Verbose) |
| `--rerun-failed` | Rerun only failed tests from test-fails.json |

### Test Scope Options

| Scope | Description |
| ----- | ----------- |
| `All` | Run all issues (default) |
| `New` | Run only issues that haven't been tested yet (no entry in results.json or test_result is null/empty) |
| `NewAndFailed` | Run issues that are new OR previously failed |
| `RegressionOnly` | Run only closed issues (regression tests) |
| `OpenOnly` | Run only open issues |

### Package Feed Options

| Feed | Description |
| ---- | ----------- |
| `Stable` | nuget.org with stable packages only (default) |
| `Beta` | nuget.org with prerelease packages enabled |
| `Alpha` | nuget.org + MyGet feed with prerelease packages enabled |
| `Local` | nuget.org + C:\nuget feed with prerelease packages enabled |

### Execution Mode Filter

| Mode | Description |
| ---- | ----------- |
| `All` | Run all issues regardless of execution method (default) |
| `Direct` | Run only issues that use direct `dotnet test` execution |
| `Custom` | Run only issues that use custom scripts |

### Test Result Files

IssueRunner automatically maintains two JSON files tracking test results:

- `test-passes.json`: Contains all tests that have passed
- `test-fails.json`: Contains all tests that have failed

These files are updated after each test run. Tests that pass after being in `test-fails.json` are automatically promoted to `test-passes.json`.

### Rerunning Failed Tests

The `--rerun-failed` option allows you to rerun only tests that previously failed:

- Reads the list of failed tests from `test-fails.json`
- Runs only those specific issue/project combinations that failed
- Automatically promotes tests that pass on rerun to `test-passes.json`
- Works independently of the `--scope` option

## Reporting

Generate a test report from your test results:

**Using wrapper scripts (recommended):**

```cmd
# Windows
.\Tools\generate-report.cmd

# Linux/macOS
./Tools/generate-report.sh
```

**Or run IssueRunner directly:**

```bash
cd Tools/IssueRunner/bin/Release/net10.0
./IssueRunner report generate
```

The report will be generated as `TestReport.md` in the repository root. It includes:

- Summary of regression tests (closed issues) and open issues
- Package versions under test
- Test results breakdown

**Note:** The report is generated from `results.json` in the repository root. Make sure you've run tests first to generate this file.

## Advanced Topics

### Using IssueRunner Across Repositories

IssueRunner can test issues in any repository, not just nunit3-vs-adapter.issues. This is useful for testing other issue repositories like nunit.issues.

**Three ways to specify the target repository:**

1. **Navigate to the target repository** (simplest):

   ```cmd
   cd C:\repos\nunit\nunit.issues
   ..\nunit3-vs-adapter.issues\Tools\run-tests.cmd --issues 1
   ```

2. **Set ISSUERUNNER_ROOT environment variable**:

   ```cmd
   # Windows
   set ISSUERUNNER_ROOT=C:\repos\nunit\nunit.issues
   ..\nunit3-vs-adapter.issues\Tools\run-tests.cmd --issues 1

   # Linux/macOS
   export ISSUERUNNER_ROOT=/home/user/repos/nunit.issues
   ../nunit3-vs-adapter.issues/Tools/run-tests.sh --issues 1
   ```

3. **Use --root parameter explicitly**:

   ```cmd
   IssueRunner run --root C:\repos\nunit\nunit.issues --issues 1
   ```

All wrapper scripts (run-tests, sync-from-github, sync-to-folders) support these methods.

### Custom Test Scripts

Issues can be executed in two ways:

1. **Direct execution**: `dotnet test` is run directly on the project file. This is the default method when no custom scripts are found.
2. **Custom script execution**: If `run_*.cmd` (Windows) or `run_*.sh` (Linux/macOS) files exist in the issue folder, those scripts are executed instead.

Custom scripts are useful when:

- An issue has multiple projects and you want to limit which ones are tested
- Special test execution logic is required
- Specific test filters need to be applied

#### Cross-Platform Compatibility

For issues that use custom test scripts, you **must provide both** `.cmd` (Windows) and `.sh` (Linux/macOS) versions with the same name:

- Windows environments use the `.cmd` files
- Linux environments use the `.sh` files
- If only one version exists, tests will fail on the other platform

Both files should contain equivalent commands - only the comment syntax differs (`REM` for `.cmd`, `#` for `.sh`).

#### Script Examples

##### Example 1: Filtering specific tests (Issue919)

Windows (`run_test_0.cmd`):

```cmd
REM EXPECT_TESTS=0
dotnet test --filter "FullyQualifiedName~Bar\(1\)"
```

Linux/macOS (`run_test_0.sh`):

```bash
# EXPECT_TESTS=0
dotnet test --filter "FullyQualifiedName~Bar\(1\)"
```

##### Example 2: Running with runsettings and filters (Issue1146)

```cmd
dotnet test NUnitFilterSample.csproj -c Release -s .runsettings --filter "TestCategory!=Sample" --logger "Console;verbosity=normal"
```

##### Example 3: Multiple scripts for different test scenarios

You can create multiple scripts (e.g., `run_test_0.cmd`, `run_test_1.cmd`) to test different scenarios. All matching scripts will be executed in alphabetical order.

### Script Metadata

You can add expectation metadata as comments in the first 10 lines of your script:

| Metadata | Description |
| -------- | ----------- |
| `EXPECT_TESTS=N` | Expected total number of tests |
| `EXPECT_PASS=N` | Expected number of passing tests |
| `EXPECT_FAIL=N` | Expected number of failing tests |
| `EXPECT_SKIP=N` | Expected number of skipped tests |

If the actual results don't match the expectations, the test run will be marked as failed.

**Example with expectations:**

```cmd
REM EXPECT_TESTS=1
REM EXPECT_PASS=1
dotnet test --filter "FullyQualifiedName~Baz\(1\)"
```

### Reset Command

The `reset` command restores projects to their original state from metadata:

- Resets **TargetFramework(s)** to original values
- Resets **package versions** to original values
- Converts between `<TargetFramework>` (singular) and `<TargetFrameworks>` (plural) as needed
- Useful after testing with different feeds or when projects get out of sync

**Usage:**

```bash
./IssueRunner reset                    # Reset all issues
./IssueRunner reset --issues 228,711   # Reset specific issues
```

**Wrapper scripts:**

```cmd
# Windows
.\Tools\reset-packages.cmd

# Linux/macOS
./Tools/reset-packages.sh
```

### Advanced CLI Examples

```bash
# Run new issues and previously failed tests
./IssueRunner run --scope NewAndFailed

# Rerun only failed tests from test-fails.json
./IssueRunner run --rerun-failed

# Run only custom script tests
./IssueRunner run --execution-mode Custom

# Test with beta/prerelease packages
./IssueRunner run --feed Beta --issues 1039

# Test with alpha packages from MyGet
./IssueRunner run --feed Alpha --issues 228

# Test with local packages from C:\nuget
./IssueRunner run --feed Local --issues 228
```

### Other Commands

```bash
# Sync metadata from GitHub (or use sync-from-github.cmd/sh)
./IssueRunner metadata sync-from-github

# Distribute metadata to issue folders (or use sync-to-folders.cmd/sh)
./IssueRunner metadata sync-to-folders

# Generate test report
./IssueRunner report generate

# Check for regression failures (CI)
./IssueRunner report check-regressions

# Merge results from multiple runs
./IssueRunner merge --linux <path> --windows <path>
```

## Maintenance

### Syncing Metadata from GitHub

Use the convenient wrapper scripts:

**Windows:**

```cmd
cd C:\repos\nunit\nunit.issues
..\nunit3-vs-adapter.issues\Tools\sync-from-github.cmd
..\nunit3-vs-adapter.issues\Tools\sync-to-folders.cmd
```

**Linux/macOS:**

```bash
cd /home/user/repos/nunit.issues
../nunit3-vs-adapter.issues/Tools/sync-from-github.sh
../nunit3-vs-adapter.issues/Tools/sync-to-folders.sh
```

Or run IssueRunner directly:

```bash
cd Tools/IssueRunner/bin/Release/net10.0

# Sync from GitHub
./IssueRunner metadata sync-from-github --root /path/to/repo

# Distribute to folders
./IssueRunner metadata sync-to-folders --root /path/to/repo
```

### What the Sync Commands Do

1. **sync-from-github**: Fetches current issue metadata from GitHub API and updates `Tools/issues_metadata.json`
   - Requires `GITHUB_TOKEN` environment variable for higher rate limits
   - Reads repository configuration from `Tools/repository.json`

2. **sync-to-folders**: Reads from `Tools/issues_metadata.json` and creates/updates `issue_metadata.json` in each `Issue*` folder
   - Includes project details (csproj files, frameworks, packages)

### Repository Configuration

Create a `Tools/repository.json` file in your target repository to specify which GitHub repository to sync from:

```json
{
  "owner": "nunit",
  "name": "nunit"
}
```

If this file doesn't exist, the sync will default to `nunit/nunit3-vs-adapter` and show a warning message.

### What to Commit

- Metadata updates from the sync scripts (central and per-issue `issue_metadata.json` files) should be committed
- Test results in individual issue folders should normally NOT be committed
- `TestReport.md` at repo root CAN be committed for documentation purposes

## Extra Stuff

### Issue Folder Marker Files

To control how issues are processed, you can drop marker files into issue folders:

| Marker File | Description |
| ----------- | ----------- |
| `ignore` or `ignore.md` | Skip this issue entirely during test/update runs |
| `explicit` or `explicit.md` | Mark as explicit (must be run explicitly) |
| `wip` or `wip.md` | Work in progress - skip during normal runs |
| `gui` or `gui.md` | GUI-related issue |
| `closedasnotplanned` or `closedasnotplanned.md` | Closed as not planned |
| `windows` or `windows.md` | Windows-only issue (skipped on Linux CI) |
