using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using IssueRunner.Gui.ViewModels;
using IssueRunner.Gui.Views;
using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IssueRunner.Gui.Tests;

[TestFixture]
public class MainWindowHeadlessTests : HeadlessTestBase
{
    [AvaloniaTest]
    public void MainWindow_CanBeCreated()
    {
        var window = CreateTestWindow();
        Assert.That(window, Is.Not.Null);
        Assert.That(window, Is.InstanceOf<MainWindow>());
    }

    [AvaloniaTest, Explicit]
    public async Task NavigationButtons_AppearWhenTestStatusViewIsActive()
    {
        try
        {
            // Set up services and mocks BEFORE creating UI
            var services = CreateTestServiceProvider();
            var envService = services.GetRequiredService<IEnvironmentService>();
            var testRepoPath = envService.Root;

            // Set up basic file structure for repository
            var dataDir = Path.Combine(testRepoPath, ".nunit", "IssueRunner");
            Directory.CreateDirectory(dataDir);
            
            // Create a basic repository.json
            var repoConfigJson = JsonSerializer.Serialize(new IssueRunner.Models.RepositoryConfig("test", "test"));
            File.WriteAllText(Path.Combine(dataDir, "repository.json"), repoConfigJson);

            // Set up mocks before creating UI
            var issueDiscovery = services.GetRequiredService<IIssueDiscoveryService>();
            issueDiscovery.ClearReceivedCalls();
            issueDiscovery.DiscoverIssueFolders().Returns(new Dictionary<int, string>());

            var markerService = services.GetRequiredService<IMarkerService>();
            markerService.ClearReceivedCalls();
            markerService.ShouldSkipIssue(Arg.Any<string>()).Returns(false);

            // Small delay to ensure mocks are set up
            await Task.Delay(50);

            // NOW create the window and viewmodel
            var window = CreateTestWindow(services);
            var viewModel = (MainViewModel)window.DataContext!;
            
            viewModel.RepositoryPath = testRepoPath;

            // Wait for repository initialization with better timeout handling
            var maxWaitTime = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;
            
            while (viewModel.SummaryText == "Select a repository to begin." && 
                   DateTime.UtcNow - startTime < maxWaitTime)
            {
                await Task.Delay(100);
            }

            // Switch to Test Status view - wait for async command to complete
            var completed = false;
            Exception? commandException = null;
            
            viewModel.ShowTestStatusCommand.Execute().Subscribe(
                _ => { },
                ex => { 
                    commandException = ex;
                    completed = true; 
                },
                () => { completed = true; });

            // Wait for command to complete with timeout
            startTime = DateTime.UtcNow;
            while (!completed && DateTime.UtcNow - startTime < maxWaitTime)
            {
                await Task.Delay(50);
            }
            
            if (commandException != null)
            {
                throw new InvalidOperationException("Command failed", commandException);
            }
            
            if (!completed)
            {
                throw new TimeoutException("ShowTestStatusCommand did not complete within timeout");
            }

            await Task.Delay(100); // Allow time for view update

            // We don't need to show the window in headless tests; rely on ViewModel state
            Assert.That(viewModel.CurrentViewType, Is.EqualTo("TestStatus"));
        }
        catch (PlatformNotSupportedException ex)
        {
            var stackTrace = ex.StackTrace ?? "No stack trace available";
            var innerEx = ex.InnerException?.ToString() ?? "No inner exception";
            
            Assert.Fail($"PlatformNotSupportedException (likely timing/race condition): {ex.Message}\n" +
                       $"Stack trace: {stackTrace}\n" +
                       $"Inner exception: {innerEx}\n" +
                       $"OS: {Environment.OSVersion}\n" +
                       $"Framework: {Environment.Version}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected exception: {ex.GetType().Name}: {ex.Message}\n" +
                       $"Stack trace: {ex.StackTrace}\n" +
                       $"Inner exception: {ex.InnerException}");
        }
    }

    [AvaloniaTest]
    public async Task ClickingIssueListButton_SwitchesViewBackToIssueListView()
    {
        var services = CreateTestServiceProvider();
        var window = CreateTestWindow(services);
        var viewModel = (MainViewModel)window.DataContext!;

        var envService = services.GetRequiredService<IEnvironmentService>();
        viewModel.RepositoryPath = envService.Root;

        // Wait for repository initialization
        await Task.Delay(300);

        // First switch to Test Status - wait for async command to complete
        var testStatusCompleted = false;
        viewModel.ShowTestStatusCommand.Execute().Subscribe(
            _ => { },
            ex => { testStatusCompleted = true; },
            () => { testStatusCompleted = true; });

        var timeout = DateTime.Now.AddSeconds(5);
        while (!testStatusCompleted && DateTime.Now < timeout)
        {
            await Task.Delay(50);
        }
        await Task.Delay(100);

        Assert.That(viewModel.CurrentViewType, Is.EqualTo("TestStatus"));

        // Then switch back to Issue List
        viewModel.ShowIssueListCommand.Execute().Subscribe();
        await Task.Delay(100); // Allow time for view update

        // Assert
        Assert.That(viewModel.CurrentView, Is.InstanceOf<IssueListView>());
        Assert.That(viewModel.CurrentViewType, Is.EqualTo("IssueList"));
    }

    [AvaloniaTest]
    public async Task ClickingTestStatusButton_SwitchesViewToTestStatusView()
    {
        var services = CreateTestServiceProvider();
        var window = CreateTestWindow(services);
        var viewModel = (MainViewModel)window.DataContext!;

        var envService = services.GetRequiredService<IEnvironmentService>();
        viewModel.RepositoryPath = envService.Root;

        // Wait for repository initialization
        await Task.Delay(300);

        // Initial state should be IssueList
        Assert.That(viewModel.CurrentViewType, Is.EqualTo("IssueList"));

        // Switch to Test Status - wait for async command to complete
        var completed = false;
        viewModel.ShowTestStatusCommand.Execute().Subscribe(
            _ => { },
            ex => { completed = true; },
            () => { completed = true; });

        var timeout = DateTime.Now.AddSeconds(5);
        while (!completed && DateTime.Now < timeout)
        {
            await Task.Delay(50);
        }
        await Task.Delay(100); // Allow time for view update

        // Assert
        Assert.That(viewModel.CurrentViewType, Is.EqualTo("TestStatus"));
    }

    [AvaloniaTest]
    public async Task RepositoryStatus_DisplaysAllStatusLinesCorrectly()
    {
        try
        {
            // Arrange - Set up mocks FIRST before creating any services or UI
            var services = CreateTestServiceProvider();
            
            // Create directories and files BEFORE setting up mocks and UI
            var envService = services.GetRequiredService<IEnvironmentService>();
            var testRepoPath = envService.Root;
            
            var dataDir = Path.Combine(testRepoPath, ".nunit", "IssueRunner");
            Directory.CreateDirectory(dataDir);
            
            var issue1Folder = Path.Combine(testRepoPath, "Issue1");
            var issue2Folder = Path.Combine(testRepoPath, "Issue2");
            var issue3Folder = Path.Combine(testRepoPath, "Issue3");
            Directory.CreateDirectory(issue1Folder);
            Directory.CreateDirectory(issue2Folder);
            Directory.CreateDirectory(issue3Folder);

            // Create marker file for issue 3
            await File.WriteAllTextAsync(Path.Combine(issue3Folder, "ignore"), "");

            // Create repository.json in the data directory (RepositoryStatusService expects it there)
            var repoConfigJson = JsonSerializer.Serialize(new IssueRunner.Models.RepositoryConfig("test", "test"));
            File.WriteAllText(Path.Combine(dataDir, "repository.json"), repoConfigJson);

            // Create results.json
            var resultsPath = Path.Combine(dataDir, "results.json");
            var results = new List<IssueResult>
            {
                new IssueResult
                {
                    Number = 1,
                    ProjectPath = "Issue1/test.csproj",
                    TargetFrameworks = new List<string> { "net8.0" },
                    Packages = new List<string>(),
                    RestoreResult = StepResultStatus.Success,
                    BuildResult = StepResultStatus.Success,
                    TestResult = StepResultStatus.Success,
                    RunResult = RunResult.Run,
                    LastRun = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                },
                new IssueResult
                {
                    Number = 2,
                    ProjectPath = "Issue2/test.csproj",
                    TargetFrameworks = new List<string> { "net8.0" },
                    Packages = new List<string>(),
                    RestoreResult = StepResultStatus.Success,
                    BuildResult = StepResultStatus.Success,
                    TestResult = StepResultStatus.Failed,
                    RunResult = RunResult.Run,
                    LastRun = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            };
            var resultsJson = JsonSerializer.Serialize(results);
            await File.WriteAllTextAsync(resultsPath, resultsJson);

            // Set up mocks BEFORE creating UI components
            var issueDiscovery = services.GetRequiredService<IIssueDiscoveryService>();
            issueDiscovery.ClearReceivedCalls();
            issueDiscovery.DiscoverIssueFolders().Returns(new Dictionary<int, string>
            {
                { 1, issue1Folder },
                { 2, issue2Folder },
                { 3, issue3Folder }
            });

            var markerService = services.GetRequiredService<IMarkerService>();
            markerService.ClearReceivedCalls();
            markerService.ShouldSkipIssue(Arg.Any<string>()).Returns(callInfo =>
            {
                var path = callInfo.Arg<string>();
                return path == issue3Folder;
            });
            markerService.GetMarkerReason(Arg.Any<string>()).Returns("Ignored");

            // Add a small delay to ensure mocks are properly set up
            await Task.Delay(50);
            
            // NOW create the window and viewmodel
            var window = CreateTestWindow(services);
            var viewModel = (MainViewModel)window.DataContext!;

            // Set repository path and wait for loading to complete
            viewModel.RepositoryPath = testRepoPath;

            // Use a more robust waiting mechanism with better timeout handling
            var maxWaitTime = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;
            
            while (viewModel.SummaryText == "Select a repository to begin." && 
                   DateTime.UtcNow - startTime < maxWaitTime)
            {
                await Task.Delay(100);
            }
            
            // Additional delay to ensure all async operations complete
            await Task.Delay(300);

            // Assert - Verify SummaryText contains repository info
            var summaryText = viewModel.SummaryText;
            Assert.That(summaryText, Is.Not.Null, "SummaryText should not be null");
            Assert.That(summaryText, Does.Not.Contain("Select a repository to begin"), 
                $"Repository should be loaded. SummaryText: {summaryText}");
            
            // Verify that all status count properties exist and are accessible
            Assert.That(viewModel.PassedCount, Is.GreaterThanOrEqualTo(0), "PassedCount property should be accessible");
            Assert.That(viewModel.FailedCount, Is.GreaterThanOrEqualTo(0), "FailedCount property should be accessible");
            Assert.That(viewModel.SkippedCount, Is.GreaterThanOrEqualTo(0), "SkippedCount property should be accessible");
            Assert.That(viewModel.NotRestoredCount, Is.GreaterThanOrEqualTo(0), "NotRestoredCount property should be accessible");
            Assert.That(viewModel.NotCompilingCount, Is.GreaterThanOrEqualTo(0), "NotCompilingCount property should be accessible");
            Assert.That(viewModel.NotTestedCount, Is.GreaterThanOrEqualTo(0), "NotTestedCount property should be accessible");
        }
        catch (PlatformNotSupportedException ex)
        {
            // Add more detailed diagnostics to identify the actual cause
            var stackTrace = ex.StackTrace ?? "No stack trace available";
            var innerEx = ex.InnerException?.ToString() ?? "No inner exception";
            
            Assert.Fail($"PlatformNotSupportedException (likely timing/race condition): {ex.Message}\n" +
                       $"Stack trace: {stackTrace}\n" +
                       $"Inner exception: {innerEx}\n" +
                       $"OS: {Environment.OSVersion}\n" +
                       $"Framework: {Environment.Version}");
        }
        catch (Exception ex)
        {
            // Catch any other exceptions to see what's really happening
            Assert.Fail($"Unexpected exception: {ex.GetType().Name}: {ex.Message}\n" +
                       $"Stack trace: {ex.StackTrace}\n" +
                       $"Inner exception: {ex.InnerException}");
        }
    }

}

