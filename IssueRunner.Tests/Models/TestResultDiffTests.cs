using IssueRunner.Models;

namespace IssueRunner.Tests.Models;

[TestFixture]
public class TestResultDiffTests
{
    [Test]
    public void Create_WithAllProperties()
    {
        // Arrange & Act
        var diff = new TestResultDiff
        {
            IssueNumber = 228,
            ProjectPath = "Test.csproj",
            BaselineStatus = StepResultStatus.Success,
            CurrentStatus = StepResultStatus.Failed,
            ChangeType = ChangeType.Regression
        };

        // Assert
        Assert.That(diff.IssueNumber, Is.EqualTo(228));
        Assert.That(diff.ProjectPath, Is.EqualTo("Test.csproj"));
        Assert.That(diff.BaselineStatus, Is.EqualTo(StepResultStatus.Success));
        Assert.That(diff.CurrentStatus, Is.EqualTo(StepResultStatus.Failed));
        Assert.That(diff.ChangeType, Is.EqualTo(ChangeType.Regression));
    }

    [Test]
    public void ChangeType_Enum_HasAllValues()
    {
        // Assert
        Assert.That(Enum.GetValues<ChangeType>(), Contains.Item(ChangeType.None));
        Assert.That(Enum.GetValues<ChangeType>(), Contains.Item(ChangeType.Fixed));
        Assert.That(Enum.GetValues<ChangeType>(), Contains.Item(ChangeType.Regression));
        Assert.That(Enum.GetValues<ChangeType>(), Contains.Item(ChangeType.BuildToFail));
        Assert.That(Enum.GetValues<ChangeType>(), Contains.Item(ChangeType.Skipped));
        Assert.That(Enum.GetValues<ChangeType>(), Contains.Item(ChangeType.Other));
    }
}
