using ChDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChDb.Tests;

[TestClass]
public class ChDbConnectionTests
{
    [TestMethod]
    public void Connect_InMemory_Succeeds()
    {
        using var conn = new ChDbConnection(":memory:");
        Assert.IsNotNull(conn);
    }

    [TestMethod]
    public void Connect_DefaultPath_Succeeds()
    {
        using var conn = new ChDbConnection();
        Assert.IsNotNull(conn);
    }

    [TestMethod]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var conn = new ChDbConnection(":memory:");
        conn.Dispose();
        conn.Dispose(); // Should not throw
    }

    [TestMethod]
    [ExpectedException(typeof(ObjectDisposedException))]
    public void Query_AfterDispose_ThrowsObjectDisposed()
    {
        var conn = new ChDbConnection(":memory:");
        conn.Dispose();
        conn.Query("SELECT 1");
    }
}
