using ChDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChDb.Tests;

[TestClass]
public class ChDbQueryTests
{
    private ChDbConnection _conn = null!;

    [TestInitialize]
    public void Setup()
    {
        _conn = new ChDbConnection(":memory:");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _conn?.Dispose();
    }

    [TestMethod]
    public void Query_SelectLiteral_ReturnsCorrectValue()
    {
        using var result = _conn.Query("SELECT 42", "CSV");
        Assert.IsFalse(result.HasError, $"Unexpected error: {result.ErrorMessage}");
        Assert.AreEqual("42\n", result.GetString());
    }

    [TestMethod]
    public void Query_SelectStringLiteral_ReturnsQuotedCSV()
    {
        using var result = _conn.Query("SELECT 'hello world'", "CSV");
        Assert.IsFalse(result.HasError);
        Assert.AreEqual("\"hello world\"\n", result.GetString());
    }

    [TestMethod]
    public void Query_SelectMultipleColumns_ReturnsCSV()
    {
        using var result = _conn.Query("SELECT 1, 2, 3", "CSV");
        Assert.IsFalse(result.HasError);
        Assert.AreEqual("1,2,3\n", result.GetString());
    }

    [TestMethod]
    public void Query_Arithmetic_ReturnsCorrectResult()
    {
        using var result = _conn.Query("SELECT 10 + 20", "CSV");
        Assert.IsFalse(result.HasError);
        Assert.AreEqual("30\n", result.GetString());
    }

    [TestMethod]
    public void Query_TSVFormat_ReturnsTabSeparated()
    {
        using var result = _conn.Query("SELECT 1, 2, 3", "TSV");
        Assert.IsFalse(result.HasError);
        Assert.AreEqual("1\t2\t3\n", result.GetString());
    }

    [TestMethod]
    public void Query_JSONFormat_ReturnsJSON()
    {
        using var result = _conn.Query("SELECT 42 AS value", "JSON");
        Assert.IsFalse(result.HasError);
        string json = result.GetString();
        Assert.IsTrue(json.Contains("\"value\":42"), $"Unexpected JSON: {json}");
    }

    [TestMethod]
    public void QueryString_ReturnsStringDirectly()
    {
        string output = _conn.QueryString("SELECT 100", "CSV");
        Assert.AreEqual("100\n", output);
    }

    [TestMethod]
    public void Query_InvalidSQL_ReturnsError()
    {
        using var result = _conn.Query("INVALID SQL STATEMENT");
        Assert.IsTrue(result.HasError, "Expected error for invalid SQL");
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    [ExpectedException(typeof(ChDbException))]
    public void QueryString_InvalidSQL_Throws()
    {
        _conn.QueryString("NOT A REAL QUERY");
    }

    [TestMethod]
    public void Query_ResultMetadata_IsPopulated()
    {
        using var result = _conn.Query("SELECT 1, 2, 3", "CSV");
        Assert.IsFalse(result.HasError);
        Assert.IsTrue(result.RowsRead > 0, "RowsRead should be > 0");
        Assert.IsTrue(result.BytesRead > 0, "BytesRead should be > 0");
        Assert.IsTrue(result.Elapsed >= 0, "Elapsed should be >= 0");
        Assert.IsTrue(result.Length > 0, "Length should be > 0");
    }
}
