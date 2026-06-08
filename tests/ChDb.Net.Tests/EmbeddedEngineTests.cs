using ChDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChDb.Tests;

/// <summary>
/// Additional coverage for the translated embedded engine logic: output formats,
/// expression evaluation, WHERE comparisons, DESCRIBE, and NULL padding.
/// Uses unique table names because the engine is a process-wide singleton.
/// </summary>
[TestClass]
public class EmbeddedEngineTests
{
    private ChDbConnection _conn = null!;

    [TestInitialize]
    public void Setup() => _conn = new ChDbConnection(":memory:");

    [TestCleanup]
    public void Cleanup() => _conn?.Dispose();

    [TestMethod]
    public void Select_StringLiteral_Json_EmitsQuotedString()
    {
        string json = _conn.QueryString("SELECT 'hi' AS greeting", "JSON");
        Assert.AreEqual("{\"greeting\":\"hi\"}\n", json);
    }

    [TestMethod]
    public void Select_Pretty_RendersBorderedTable()
    {
        _conn.QueryString("CREATE TABLE eng_pretty (id Int64)");
        _conn.QueryString("INSERT INTO eng_pretty VALUES (7)");

        string output = _conn.QueryString("SELECT * FROM eng_pretty", "Pretty");
        StringAssert.Contains(output, "+");
        StringAssert.Contains(output, "| id");
        StringAssert.Contains(output, "| 7");
    }

    [TestMethod]
    public void Select_Subtraction_ComputesValue()
    {
        Assert.AreEqual("8\n", _conn.QueryString("SELECT 10 - 2", "CSV"));
    }

    [TestMethod]
    public void Select_Multiplication_ComputesValue()
    {
        Assert.AreEqual("12\n", _conn.QueryString("SELECT 3 * 4", "CSV"));
    }

    [TestMethod]
    public void Select_IntegerDivision_TruncatesLikeCpp()
    {
        Assert.AreEqual("3\n", _conn.QueryString("SELECT 7 / 2", "CSV"));
    }

    [TestMethod]
    public void Select_DivideByZero_ReturnsNull()
    {
        // Integer divide-by-zero yields a NULL value, rendered as NULL in CSV.
        Assert.AreEqual("NULL\n", _conn.QueryString("SELECT 5 / 0", "CSV"));
    }

    [TestMethod]
    public void Select_TrailingSemicolon_IsStripped()
    {
        Assert.AreEqual("1\n", _conn.QueryString("SELECT 1;", "CSV"));
    }

    [TestMethod]
    public void Describe_ListsColumnsAndTypes()
    {
        _conn.QueryString("CREATE TABLE eng_desc (id Int64, label String)");
        string output = _conn.QueryString("DESCRIBE eng_desc", "CSV");
        StringAssert.Contains(output, "id");
        StringAssert.Contains(output, "Int64");
        StringAssert.Contains(output, "label");
        StringAssert.Contains(output, "String");
    }

    [TestMethod]
    public void Insert_FewerValuesThanColumns_PadsWithNull()
    {
        _conn.QueryString("CREATE TABLE eng_pad (a Int64, b Int64, c String)");
        _conn.QueryString("INSERT INTO eng_pad VALUES (1, 2)");

        string output = _conn.QueryString("SELECT * FROM eng_pad", "CSV");
        Assert.AreEqual("1,2,NULL\n", output);
    }

    [TestMethod]
    public void Select_WhereEquals_FiltersByString()
    {
        _conn.QueryString("CREATE TABLE eng_people (id Int64, name String)");
        _conn.QueryString("INSERT INTO eng_people VALUES (1, 'Alice'), (2, 'Bob')");

        string output = _conn.QueryString("SELECT * FROM eng_people WHERE name = 'Bob'", "CSV");
        Assert.AreEqual("2,\"Bob\"\n", output);
    }

    [TestMethod]
    public void Select_WhereLessThanOrEqual_FiltersByInt()
    {
        _conn.QueryString("CREATE TABLE eng_nums (v Int64)");
        _conn.QueryString("INSERT INTO eng_nums VALUES (1), (5), (10)");

        string output = _conn.QueryString("SELECT * FROM eng_nums WHERE v <= 5", "CSV");
        Assert.AreEqual("1\n5\n", output);
    }

    [TestMethod]
    public void Select_ProjectedColumn_WithAlias_UsesAliasName()
    {
        _conn.QueryString("CREATE TABLE eng_alias (id Int64)");
        _conn.QueryString("INSERT INTO eng_alias VALUES (42)");

        string json = _conn.QueryString("SELECT id AS the_id FROM eng_alias", "JSON");
        Assert.AreEqual("{\"the_id\":42}\n", json);
    }

    [TestMethod]
    public void Insert_MultipleTuples_AppendsAllRows()
    {
        _conn.QueryString("CREATE TABLE eng_multi (v Int64)");
        _conn.QueryString("INSERT INTO eng_multi VALUES (1), (2), (3)");

        using ChDbResult result = _conn.Query("SELECT * FROM eng_multi", "CSV");
        Assert.AreEqual(3ul, result.RowsRead);
        Assert.AreEqual("1\n2\n3\n", result.GetString());
    }

    [TestMethod]
    public void DropTableIfExists_ExistingTable_RemovesIt()
    {
        _conn.QueryString("CREATE TABLE eng_drop (v Int64)");
        _conn.QueryString("DROP TABLE IF EXISTS eng_drop");

        using ChDbResult result = _conn.Query("SELECT * FROM eng_drop", "CSV");
        Assert.IsTrue(result.HasError);
    }

    [TestMethod]
    public void Select_TabSeparatedFormat_UsesTabs()
    {
        Assert.AreEqual("1\t2\n", _conn.QueryString("SELECT 1, 2", "TSV"));
    }
}
