using ChDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChDb.Tests;

[TestClass]
public class ChDbEngineTests
{
    [TestMethod]
    public void Version_ReturnsNonEmpty()
    {
        string version = ChDbEngine.Version;
        Assert.IsFalse(string.IsNullOrWhiteSpace(version), "Version should not be empty");
        Assert.AreEqual("1.0.0", version);
    }

    [TestMethod]
    public void QueryCommandLine_SelectLiteral_ReturnsResult()
    {
        using var result = ChDbEngine.QueryCommandLine("chdb", "--query=SELECT 1");
        Assert.IsFalse(result.HasError, $"Error: {result.ErrorMessage}");
        string output = result.GetString();
        Assert.IsTrue(output.Contains("1"), $"Expected 1 in output: {output}");
    }

    [TestMethod]
    public void QueryCommandLine_WithFormat_ReturnsFormatted()
    {
        using var result = ChDbEngine.QueryCommandLine("chdb", "--query=SELECT 42 AS answer", "--output-format=JSON");
        Assert.IsFalse(result.HasError, $"Error: {result.ErrorMessage}");
        string output = result.GetString();
        Assert.IsTrue(output.Contains("\"answer\":42"), $"Expected JSON output, got: {output}");
    }

    [TestMethod]
    public void QueryCommandLine_NoQuery_ReturnsError()
    {
        using var result = ChDbEngine.QueryCommandLine("chdb");
        Assert.IsTrue(result.HasError, "Expected error when no query specified");
    }
}
