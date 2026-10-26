using IssueRunner.Commands;
using IssueRunner.Models;
using IssueRunner.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace IssueRunner.Tests;

[TestFixture]
public class UpdateNewIssuesCommandTests
{
    private IInitialStateService _initialStateService = null!;
    private IIssueDiscoveryService _issueDiscovery = null!;
    private ILogger<UpdateNewIssuesCommand> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _initialStateService = Substitute.For<IInitialStateService>();
        _issueDiscovery = Substitute.For<IIssueDiscoveryService>();
        _logger = Substitute.For<ILogger<UpdateNewIssuesCommand>>();
    }

    [Test]
    public async Task ExecuteAsync_CallsService_AndReturnsZero_WhenNoErrors()
    {
        _issueDiscovery.DiscoverIssueFolders().Returns(new Dictionary<int, string> { { 1, "/repo/Issue1" } });
        _initialStateService.ProcessAllAsync(Arg.Any<string>(), Arg.Any<List<int>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<InitialStateIssueResult>
            {
                new() { Number = 1, Status = InitialStateStatus.Ok }
            });

        var cmd = new UpdateNewIssuesCommand(_initialStateService, _issueDiscovery, _logger);
        var result = await cmd.ExecuteAsync("/repo", null, false, CancellationToken.None);

        Assert.That(result, Is.EqualTo(0));
        await _initialStateService.Received(1).ProcessAllAsync("/repo", null, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ReturnsOne_WhenServiceReportsErrors()
    {
        _issueDiscovery.DiscoverIssueFolders().Returns(new Dictionary<int, string> { { 1, "/repo/Issue1" } });
        _initialStateService.ProcessAllAsync(Arg.Any<string>(), Arg.Any<List<int>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<InitialStateIssueResult>
            {
                new() { Number = 1, Status = InitialStateStatus.Error, Messages = ["Missing state"] }
            });

        var cmd = new UpdateNewIssuesCommand(_initialStateService, _issueDiscovery, _logger);
        var result = await cmd.ExecuteAsync("/repo", null, false, CancellationToken.None);

        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_FiltersByIssueNumbers_WhenProvided()
    {
        _issueDiscovery.DiscoverIssueFolders().Returns(new Dictionary<int, string> { { 1, "/repo/Issue1" }, { 228, "/repo/Issue228" } });
        _initialStateService.ProcessAllAsync(Arg.Any<string>(), Arg.Any<List<int>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<InitialStateIssueResult> { new() { Number = 228, Status = InitialStateStatus.Ok } });

        var cmd = new UpdateNewIssuesCommand(_initialStateService, _issueDiscovery, _logger);
        await cmd.ExecuteAsync("/repo", [228], false, CancellationToken.None);

        await _initialStateService.Received(1).ProcessAllAsync("/repo", Arg.Is<List<int>?>(l => l != null && l.Count == 1 && l[0] == 228), false, Arg.Any<CancellationToken>());
    }
}
