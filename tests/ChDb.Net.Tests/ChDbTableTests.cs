using ChDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChDb.Tests;

[TestClass]
public class ChDbTableTests
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
    public void CreateTable_Succeeds()
    {
        using var result = _conn.Query("CREATE TABLE test (id Int64, name String)");
        Assert.IsFalse(result.HasError, $"Error: {result.ErrorMessage}");
    }

    [TestMethod]
    public void InsertAndSelect_ReturnsData()
    {
        _conn.QueryString("CREATE TABLE users (id Int64, name String, age Int64)");
        _conn.QueryString("INSERT INTO users VALUES (1, 'Alice', 30), (2, 'Bob', 25)");

        string output = _conn.QueryString("SELECT * FROM users", "CSV");
        Assert.IsTrue(output.Contains("Alice"), $"Expected Alice in output: {output}");
        Assert.IsTrue(output.Contains("Bob"), $"Expected Bob in output: {output}");
    }

    [TestMethod]
    public void SelectWithWhere_FiltersRows()
    {
        _conn.QueryString("CREATE TABLE products (id Int64, price Int64)");
        _conn.QueryString("INSERT INTO products VALUES (1, 10), (2, 50), (3, 100)");

        string output = _conn.QueryString("SELECT * FROM products WHERE price > 20", "CSV");
        Assert.IsFalse(output.Contains(",10\n"), "Should not contain price=10");
        Assert.IsTrue(output.Contains("50"), "Should contain price=50");
        Assert.IsTrue(output.Contains("100"), "Should contain price=100");
    }

    [TestMethod]
    public void ShowTables_ListsCreatedTables()
    {
        _conn.QueryString("CREATE TABLE alpha (x Int64)");
        _conn.QueryString("CREATE TABLE beta (y String)");

        string output = _conn.QueryString("SHOW TABLES", "CSV");
        Assert.IsTrue(output.Contains("alpha"), $"Expected alpha in: {output}");
        Assert.IsTrue(output.Contains("beta"), $"Expected beta in: {output}");
    }

    [TestMethod]
    public void DropTable_RemovesTable()
    {
        _conn.QueryString("CREATE TABLE temp (v Int64)");
        _conn.QueryString("DROP TABLE temp");

        using var result = _conn.Query("SELECT * FROM temp");
        Assert.IsTrue(result.HasError, "Should error after table is dropped");
    }

    [TestMethod]
    public void DropTableIfExists_NonExistent_Succeeds()
    {
        using var result = _conn.Query("DROP TABLE IF EXISTS nonexistent");
        Assert.IsFalse(result.HasError, $"Error: {result.ErrorMessage}");
    }
}
