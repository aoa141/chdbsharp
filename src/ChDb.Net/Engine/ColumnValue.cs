using System.Globalization;

namespace ChDb.Engine;

/// <summary>
/// The kind of value held in a <see cref="ColumnValue"/>.
/// Port of <c>ChDB::ColumnValue::Type</c> from EmbeddedEngine.h.
/// </summary>
internal enum ColumnValueType
{
    Int64,
    Float64,
    String,
    Null,
}

/// <summary>
/// Represents a single value in a result cell.
/// Port of the C++ <c>ChDB::ColumnValue</c> struct.
/// </summary>
internal readonly struct ColumnValue
{
    public ColumnValueType Type { get; }
    public long I64 { get; }
    public double F64 { get; }
    public string Str { get; }

    /// <summary>Constructs a NULL value.</summary>
    public ColumnValue()
    {
        Type = ColumnValueType.Null;
        I64 = 0;
        F64 = 0.0;
        Str = string.Empty;
    }

    public ColumnValue(long value)
    {
        Type = ColumnValueType.Int64;
        I64 = value;
        F64 = 0.0;
        Str = string.Empty;
    }

    public ColumnValue(double value)
    {
        Type = ColumnValueType.Float64;
        I64 = 0;
        F64 = value;
        Str = string.Empty;
    }

    public ColumnValue(string value)
    {
        Type = ColumnValueType.String;
        I64 = 0;
        F64 = 0.0;
        Str = value;
    }

    /// <summary>A reusable NULL value.</summary>
    public static ColumnValue Null { get; } = new ColumnValue();

    /// <summary>
    /// Renders the value as text. Mirrors <c>ColumnValue::toString()</c>.
    /// </summary>
    public override string ToString()
    {
        return Type switch
        {
            ColumnValueType.Int64 => I64.ToString(CultureInfo.InvariantCulture),
            ColumnValueType.Float64 => FormatDouble(F64),
            ColumnValueType.String => Str,
            _ => "NULL",
        };
    }

    /// <summary>
    /// Approximates C++ <c>std::ostream &lt;&lt; double</c> default formatting
    /// (general format, 6 significant digits, lowercase exponent).
    /// </summary>
    private static string FormatDouble(double value)
    {
        return value.ToString("G6", CultureInfo.InvariantCulture)
            .Replace("E", "e");
    }
}
