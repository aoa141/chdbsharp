using System.Collections.Generic;

namespace ChDb.Engine;

/// <summary>
/// Column definition (name + ClickHouse type name such as "Int64", "String").
/// Port of the C++ <c>ChDB::ColumnDef</c> struct.
/// </summary>
internal sealed class ColumnDef
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// An in-memory table. Port of the C++ <c>ChDB::TableDef</c> struct.
/// </summary>
internal sealed class TableDef
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnDef> Columns { get; } = new();
    public List<List<ColumnValue>> Rows { get; } = new();
}

/// <summary>
/// A query result row set. Port of the C++ <c>ChDB::ResultSet</c> struct.
/// </summary>
internal sealed class ResultSet
{
    public List<string> ColumnNames { get; } = new();
    public List<List<ColumnValue>> Rows { get; } = new();
}
