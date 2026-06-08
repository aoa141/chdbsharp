using ChDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChDb.Tests;

[TestClass]
public class ChDbResultTests
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
    public void GetBuffer_ReturnsNonEmptySpan()
    {
        using var result = _conn.Query("SELECT 1", "CSV");
        Assert.IsFalse(result.HasError);

        var buffer = result.GetBuffer();
        Assert.IsTrue(buffer.Length > 0, "Buffer should not be empty");
    }

    [TestMethod]
    public void Elapsed_IsNonNegative()
    {
        using var result = _conn.Query("SELECT 1", "CSV");
        Assert.IsFalse(result.HasError);
        Assert.IsTrue(result.Elapsed >= 0.0);
    }

    [TestMethod]
    public void StorageMetrics_AreAvailable()
    {
        using var result = _conn.Query("SELECT 1, 2, 3", "CSV");
        Assert.IsFalse(result.HasError);
        // For expression-only queries, storage reads equal result reads
        Assert.IsTrue(result.StorageRowsRead >= 0);
        Assert.IsTrue(result.StorageBytesRead >= 0);
    }

    [TestMethod]
    public void Dispose_ClearsHandle()
    {
        var result = _conn.Query("SELECT 1", "CSV");
        Assert.IsFalse(result.HasError);
        result.Dispose();

        // Accessing after dispose should throw
        Assert.ThrowsException<ObjectDisposedException>(() => _ = result.Length);
    }

    [TestMethod]
    public void GetString_EmptyResult_ReturnsEmptyString()
    {
        // DDL statements return empty buffers
        using var result = _conn.Query("CREATE TABLE empty_test (x Int64)");
        Assert.IsFalse(result.HasError);
        Assert.AreEqual(string.Empty, result.GetString());
    }
}
