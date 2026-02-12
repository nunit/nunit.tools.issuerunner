using IssueRunner.Models;
using System.Text.Json;

namespace IssueRunner.Tests.Models;

[TestFixture]
public class IssueProjectMetadataTests
{
    [Test]
    public void Serialize_AndDeserialize_PreservesAllProperties()
    {
        // Arrange
        var original = new IssueProjectMetadata
        {
            Number = 228,
            Title = "Test Issue",
            State = GithubIssueState.Open,
            Milestone = "v1.0",
            Labels = new List<string> { "bug", "priority:high" },
            Url = "https://github.com/test/test/issues/228"
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<IssueProjectMetadata>(json);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Number, Is.EqualTo(228));
        Assert.That(deserialized.Title, Is.EqualTo("Test Issue"));
        Assert.That(deserialized.State, Is.EqualTo(GithubIssueState.Open));
        Assert.That(deserialized.Milestone, Is.EqualTo("v1.0"));
        Assert.That(deserialized.Labels, Is.EquivalentTo(new[] { "bug", "priority:high" }));
    }

    [Test]
    public void Serialize_HandlesNullMilestone()
    {
        // Arrange
        var original = new IssueProjectMetadata
        {
            Number = 1,
            Title = "Test",
            State = GithubIssueState.Open,
            Milestone = null,
            Labels = new List<string>(),
            Url = "https://github.com/test/test/issues/1"
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<IssueProjectMetadata>(json);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Milestone, Is.Null);
    }
}
