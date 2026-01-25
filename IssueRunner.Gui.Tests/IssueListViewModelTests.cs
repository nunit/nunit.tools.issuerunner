using IssueRunner.Gui.ViewModels;
using IssueRunner.Models;
using NUnit.Framework;

namespace IssueRunner.Gui.Tests;

[TestFixture]
public class IssueListViewModelTests
{
    // Helper method to create IssueListItem with proper Metadata and Results
    private static IssueListItem CreateIssueListItem(
        int number,
        string? title = null,
        GithubIssueState? state = null,
        string? testResult = null,
        IssueState? stateValue = null,
        string? testTypes = null,
        string? framework = null,
        string? milestone = null,
        string? type = null)
    {
        var metadata = new IssueMetadata
        {
            Number = number,
            Title = title ?? $"Issue {number}",
            State = state ?? GithubIssueState.Open,
            Milestone = milestone,
            Labels = type != null ? [$"is:{type}"] : [],
            Url = $"https://github.com/test/repo/issues/{number}"
        };

        List<IssueResult>? results = null;
        if (testResult != null)
        {
            var status = testResult.ToLowerInvariant() switch
            {
                "success" => StepResultStatus.Success,
                "fail" or "failed" => StepResultStatus.Failed,
                _ => StepResultStatus.NotRun
            };
            results = new List<IssueResult>
            {
                new IssueResult
                {
                    Number = number,
                    ProjectPath = "test.csproj",
                    TargetFrameworks = [],
                    Packages = [],
                    TestResult = status,
                    LastRun = DateTime.UtcNow.ToString("O")
                }
            };
        }

        return new IssueListItem
        {
            Metadata = metadata,
            Results = results,
            StateValue = stateValue ?? IssueState.Synced,
            TestTypes = testTypes ?? "",
            Framework = framework ?? ""
        };
    }
    [Test]
    public void ApplyFilters_FiltersIssuesBySelectedTestTypes_WhenTestTypesIsScriptsOnly()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, testTypes: "Scripts"),
            CreateIssueListItem(2, testTypes: "DotNet test"),
            CreateIssueListItem(3, testTypes: "Scripts")
        };
        viewModel.LoadIssues(issues);
        
        // Act
        viewModel.SelectedTestTypes = "Scripts only";
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.All(i => i.TestTypes == "Scripts"), Is.True);
        Assert.That(viewModel.Issues.Select(i => i.Number), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void ApplyFilters_FiltersIssuesBySelectedTestTypes_WhenTestTypesIsDotnetTestOnly()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, testTypes: "Scripts"),
            CreateIssueListItem(2, testTypes: "DotNet test"),
            CreateIssueListItem(3, testTypes: "DotNet test")
        };
        viewModel.LoadIssues(issues);
        
        // Act
        viewModel.SelectedTestTypes = "dotnet test only";
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.All(i => i.TestTypes == "DotNet test"), Is.True);
        Assert.That(viewModel.Issues.Select(i => i.Number), Is.EquivalentTo(new[] { 2, 3 }));
    }

    [Test]
    public void ApplyFilters_ShowsAllIssues_WhenTestTypesIsAll()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, testTypes: "Scripts"),
            CreateIssueListItem(2, testTypes: "DotNet test"),
            CreateIssueListItem(3, testTypes: "Scripts")
        };
        viewModel.LoadIssues(issues);
        viewModel.SelectedTestTypes = "Scripts only"; // First filter to Scripts only
        
        // Act
        viewModel.SelectedTestTypes = "All";
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(3));
    }

    [Test]
    public void TestTypes_PropertyIsPopulatedCorrectly_ForIssueWithCustomScripts()
    {
        // Arrange
        var issue = CreateIssueListItem(1, "Test Issue", testTypes: "Scripts");
        
        // Assert
        Assert.That(issue.TestTypes, Is.EqualTo("Scripts"));
    }

    [Test]
    public void TestTypes_PropertyIsPopulatedCorrectly_ForIssueWithoutCustomScripts()
    {
        // Arrange
        var issue = CreateIssueListItem(1, "Test Issue", testTypes: "DotNet test");
        
        // Assert
        Assert.That(issue.TestTypes, Is.EqualTo("DotNet test"));
    }

    [Test]
    public void HasActiveFilters_ReturnsTrue_WhenTestTypesFilterIsSet()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        
        // Act
        viewModel.SelectedTestTypes = "Scripts only";
        
        // Assert
        Assert.That(viewModel.HasActiveFilters, Is.True);
    }

    [Test]
    public void HasActiveFilters_ReturnsFalse_WhenAllFiltersAreDefault()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        
        // Assert
        Assert.That(viewModel.SelectedScope, Is.EqualTo(TestScope.All));
        Assert.That(viewModel.SelectedState, Is.EqualTo("All"));
        Assert.That(viewModel.SelectedTestResult, Is.EqualTo("All"));
        Assert.That(viewModel.SelectedTestTypes, Is.EqualTo("All"));
        Assert.That(viewModel.HasActiveFilters, Is.False);
    }

    [Test]
    public void ApplyFilters_FiltersIssuesBySelectedScope_WhenScopeIsRegression()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, state: GithubIssueState.Closed),
            CreateIssueListItem(2, state: GithubIssueState.Open),
            CreateIssueListItem(3, state: GithubIssueState.Closed)
        };
        viewModel.LoadIssues(issues);
        
        // Act
        viewModel.SelectedScope = TestScope.Regression;
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.All(i => i.State == GithubIssueState.Closed), Is.True);
        Assert.That(viewModel.Issues.Select(i => i.Number), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void ApplyFilters_FiltersIssuesBySelectedScope_WhenScopeIsOpen()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, state: GithubIssueState.Closed),
            CreateIssueListItem(2, state: GithubIssueState.Open),
            CreateIssueListItem(3, state: GithubIssueState.Open)
        };
        viewModel.LoadIssues(issues);
        
        // Act
        viewModel.SelectedScope = TestScope.Open;
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.All(i => i.State == GithubIssueState.Open), Is.True);
        Assert.That(viewModel.Issues.Select(i => i.Number), Is.EquivalentTo(new[] { 2, 3 }));
    }

    [Test]
    public void ApplyFilters_ShowsAllIssues_WhenScopeIsAll()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, state: GithubIssueState.Closed),
            CreateIssueListItem(2, state: GithubIssueState.Open),
            CreateIssueListItem(3, state: GithubIssueState.Closed)
        };
        viewModel.LoadIssues(issues);
        viewModel.SelectedScope = TestScope.Regression; // First filter to Regression
        
        // Act
        viewModel.SelectedScope = TestScope.All;
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(3));
    }

    [Test]
    public void ApplyFilters_FiltersIssuesBySelectedState_WhenStateIsNew()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, stateValue: IssueState.New),
            CreateIssueListItem(2, stateValue: IssueState.Synced),
            CreateIssueListItem(3, stateValue: IssueState.New)
        };
        viewModel.LoadIssues(issues);
        
        // Act
        viewModel.SelectedState = "New";
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.All(i => i.StateValue == IssueState.New), Is.True);
        Assert.That(viewModel.Issues.Select(i => i.Number), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void ApplyFilters_FiltersIssuesBySelectedState_WhenStateIsSkipped()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, stateValue: IssueState.Skipped),
            CreateIssueListItem(2, stateValue: IssueState.Runnable),
            CreateIssueListItem(3, stateValue: IssueState.Skipped)
        };
        viewModel.LoadIssues(issues);
        
        // Act
        viewModel.SelectedState = "Skipped";
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.All(i => i.StateValue == IssueState.Skipped), Is.True);
        Assert.That(viewModel.Issues.Select(i => i.Number), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void ApplyFilters_FiltersIssuesBySelectedState_WhenStateIsFailedCompile()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, stateValue: IssueState.FailedCompile),
            CreateIssueListItem(2, stateValue: IssueState.Runnable),
            CreateIssueListItem(3, stateValue: IssueState.FailedCompile)
        };
        viewModel.LoadIssues(issues);
        
        // Act
        viewModel.SelectedState = "Failed compile";
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.All(i => i.StateValue == IssueState.FailedCompile), Is.True);
        Assert.That(viewModel.Issues.Select(i => i.Number), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void ApplyFilters_ShowsAllIssues_WhenStateIsAll()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, stateValue: IssueState.New),
            CreateIssueListItem(2, stateValue: IssueState.Synced),
            CreateIssueListItem(3, stateValue: IssueState.Runnable)
        };
        viewModel.LoadIssues(issues);
        viewModel.SelectedState = "New"; // First filter to New
        
        // Act
        viewModel.SelectedState = "All";
        
        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(3));
    }

    [Test]
    public void LoadIssues_ClearsExistingIssues()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var initialIssues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Old Title 1"),
            CreateIssueListItem(2, "Old Title 2")
        };
        viewModel.LoadIssues(initialIssues);
        Assert.That(viewModel.AllIssues.Count, Is.EqualTo(2));

        // Act
        var newIssues = new List<IssueListItem>
        {
            CreateIssueListItem(3, "New Title 3")
        };
        viewModel.LoadIssues(newIssues);

        // Assert
        Assert.That(viewModel.AllIssues.Count, Is.EqualTo(1));
        Assert.That(viewModel.AllIssues.First().Number, Is.EqualTo(3));
    }

    [Test]
    public void LoadIssues_AddsAllItemsToCollection()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1"),
            CreateIssueListItem(2, "Title 2"),
            CreateIssueListItem(3, "Title 3")
        };

        // Act
        viewModel.LoadIssues(issues);

        // Assert
        Assert.That(viewModel.AllIssues.Count, Is.EqualTo(3));
        Assert.That(viewModel.AllIssues.Select(i => i.Number), Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void LoadIssues_PreservesTitleProperty_WhenLoadingItems()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Test Title 1"),
            CreateIssueListItem(228, "Tests inherited from Generic test fixture"),
            CreateIssueListItem(999, "Issue 999")
        };

        // Act
        viewModel.LoadIssues(issues);

        // Assert
        Assert.That(viewModel.AllIssues.Count, Is.EqualTo(3));
        Assert.That(viewModel.AllIssues.First(i => i.Number == 1).Title, Is.EqualTo("Test Title 1"));
        Assert.That(viewModel.AllIssues.First(i => i.Number == 228).Title, Is.EqualTo("Tests inherited from Generic test fixture"));
        Assert.That(viewModel.AllIssues.First(i => i.Number == 999).Title, Is.EqualTo("Issue 999"));
    }

    [Test]
    public void LoadIssues_CallsApplyFilters_AfterLoading()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1", GithubIssueState.Open),
            CreateIssueListItem(2, "Title 2", GithubIssueState.Closed)
        };

        // Act
        viewModel.LoadIssues(issues);

        // Assert - Filters should be applied, so Issues collection should be populated
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_WhenNoFiltersApplied()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1"),
            CreateIssueListItem(2, "Title 2")
        };
        viewModel.LoadIssues(issues);

        // Act - No filter changes (all defaults)

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(2));
        Assert.That(viewModel.Issues.First(i => i.Number == 1).Title, Is.EqualTo("Title 1"));
        Assert.That(viewModel.Issues.First(i => i.Number == 2).Title, Is.EqualTo("Title 2"));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_WhenFilteringByScope()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1", GithubIssueState.Open),
            CreateIssueListItem(2, "Title 2", GithubIssueState.Closed)
        };
        viewModel.LoadIssues(issues);

        // Act
        viewModel.SelectedScope = TestScope.Regression;

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Number, Is.EqualTo(2));
        Assert.That(viewModel.Issues.First().Title, Is.EqualTo("Title 2"));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_WhenFilteringByState()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1", stateValue: IssueState.New),
            CreateIssueListItem(2, "Title 2", stateValue: IssueState.Synced)
        };
        viewModel.LoadIssues(issues);

        // Act
        viewModel.SelectedState = "New";

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Number, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Title, Is.EqualTo("Title 1"));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_WhenFilteringByTestResult()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1", testResult: "success"),
            CreateIssueListItem(2, "Title 2", testResult: "fail")
        };
        viewModel.LoadIssues(issues);

        // Act
        viewModel.SelectedTestResult = "Success";

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Number, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Title, Is.EqualTo("Title 1"));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_WhenFilteringByTestTypes()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1", testTypes: "Scripts"),
            CreateIssueListItem(2, "Title 2", testTypes: "DotNet test")
        };
        viewModel.LoadIssues(issues);

        // Act
        viewModel.SelectedTestTypes = "Scripts only";

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Number, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Title, Is.EqualTo("Title 1"));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_WhenFilteringByFramework()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1", framework: ".Net"),
            CreateIssueListItem(2, "Title 2", framework: ".Net Framework")
        };
        viewModel.LoadIssues(issues);

        // Act
        viewModel.SelectedFramework = ".Net";

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Number, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Title, Is.EqualTo("Title 1"));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_WhenFilteringByDiff()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1"),
            CreateIssueListItem(2, "Title 2")
        };
        viewModel.LoadIssues(issues);
        viewModel.IssueChanges = new Dictionary<string, ChangeType>
        {
            { "Issue1|test.csproj", ChangeType.Regression }
        };

        // Act
        viewModel.ViewMode = IssueViewMode.Diff;

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Number, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Title, Is.EqualTo("Title 1"));
    }

    [Test]
    public void ApplyFilters_PreservesTitle_ThroughMultipleFilterChanges()
    {
        // Arrange
        var viewModel = new IssueListViewModel();
        var issues = new List<IssueListItem>
        {
            CreateIssueListItem(1, "Title 1", GithubIssueState.Open, "success"),
            CreateIssueListItem(2, "Title 2", GithubIssueState.Closed, "fail")
        };
        viewModel.LoadIssues(issues);

        // Act - Apply multiple filters
        viewModel.SelectedScope = TestScope.Open;
        viewModel.SelectedTestResult = "Success";

        // Assert
        Assert.That(viewModel.Issues.Count, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Number, Is.EqualTo(1));
        Assert.That(viewModel.Issues.First().Title, Is.EqualTo("Title 1"));
    }
}

