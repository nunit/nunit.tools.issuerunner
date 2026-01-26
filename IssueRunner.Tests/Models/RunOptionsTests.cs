using IssueRunner.Models;
using NUnit.Framework;

namespace IssueRunner.Tests.Models;

[TestFixture]
public class RunOptionsTests
{
    [Test]
    public void TestTypes_Enum_HasCorrectValues()
    {
        // Assert
        Assert.That(Enum.IsDefined(typeof(RunType), RunType.All), Is.True);
        Assert.That(Enum.IsDefined(typeof(RunType), RunType.DotNet), Is.True);
        Assert.That(Enum.IsDefined(typeof(RunType), RunType.Script), Is.True);
    }

    [Test]
    public void RunOptions_TestTypes_PropertyInitializesToAll()
    {
        // Arrange & Act
        var options = new RunOptions();
        
        // Assert
        Assert.That(options.RunType, Is.EqualTo(RunType.All));
    }

    [Test]
    public void RunOptions_TestTypes_CanBeSetToDirect()
    {
        // Arrange & Act
        var options = new RunOptions
        {
            RunType = RunType.DotNet
        };
        
        // Assert
        Assert.That(options.RunType, Is.EqualTo(RunType.DotNet));
    }

    [Test]
    public void RunOptions_TestTypes_CanBeSetToCustom()
    {
        // Arrange & Act
        var options = new RunOptions
        {
            RunType = RunType.Script
        };
        
        // Assert
        Assert.That(options.RunType, Is.EqualTo(RunType.Script));
    }

    [Test]
    public void RunOptions_ExecutionMode_PropertyDoesNotExist()
    {
        // Arrange
        var runOptionsType = typeof(RunOptions);
        
        // Act & Assert
        var executionModeProperty = runOptionsType.GetProperty("ExecutionMode");
        Assert.That(executionModeProperty, Is.Null, "ExecutionMode property should not exist - it was renamed to RunType");
    }
}


