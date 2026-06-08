using System.Text;

namespace ChDb.Engine;

/// <summary>
/// Discriminates the kind of <see cref="QueryResult"/>.
/// Port of the C++ <c>ChDB::QueryResultType</c> enum.
/// </summary>
internal enum QueryResultType : byte
{
    Materialized = 0,
    Streaming = 1,
    None = 4,
}

/// <summary>
/// Base class for query results. Port of the C++ <c>ChDB::QueryResult</c>.
/// </summary>
internal abstract class QueryResult
{
    protected QueryResult(QueryResultType type, string errorMessage = "")
    {
        ResultType = type;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public QueryResultType ResultType { get; }

    /// <summary>Error description, or empty string when the query succeeded.</summary>
    public string ErrorMessage { get; }

    public abstract bool IsEmpty { get; }
}

/// <summary>
/// A streaming query result. Port of the C++ <c>ChDB::StreamQueryResult</c>.
/// Streaming is materialized on this platform, so this type is rarely produced.
/// </summary>
internal sealed class StreamQueryResult : QueryResult
{
    public StreamQueryResult(string errorMessage = "")
        : base(QueryResultType.Streaming, errorMessage)
    {
    }

    public override bool IsEmpty => false;
}

/// <summary>
/// A fully materialized query result holding the formatted output buffer plus
/// execution statistics. Port of the C++ <c>ChDB::MaterializedQueryResult</c>.
/// </summary>
internal sealed class MaterializedQueryResult : QueryResult
{
    /// <summary>Constructs a successful result.</summary>
    public MaterializedQueryResult(
        byte[]? resultBuffer,
        double elapsed,
        ulong rowsRead,
        ulong bytesRead,
        ulong storageRowsRead,
        ulong storageBytesRead)
        : base(QueryResultType.Materialized)
    {
        ResultBuffer = resultBuffer;
        Elapsed = elapsed;
        RowsRead = rowsRead;
        BytesRead = bytesRead;
        StorageRowsRead = storageRowsRead;
        StorageBytesRead = storageBytesRead;
    }

    /// <summary>Constructs an error result.</summary>
    public MaterializedQueryResult(string errorMessage)
        : base(QueryResultType.Materialized, errorMessage)
    {
        ResultBuffer = null;
        Elapsed = 0.0;
        RowsRead = 0;
        BytesRead = 0;
        StorageRowsRead = 0;
        StorageBytesRead = 0;
    }

    public byte[]? ResultBuffer { get; }
    public double Elapsed { get; }
    public ulong RowsRead { get; }
    public ulong BytesRead { get; }
    public ulong StorageRowsRead { get; }
    public ulong StorageBytesRead { get; }

    public override bool IsEmpty => RowsRead == 0;

    /// <summary>Returns the buffer contents as a UTF-8 string.</summary>
    public string AsString()
    {
        return ResultBuffer is null ? string.Empty : Encoding.UTF8.GetString(ResultBuffer);
    }
}
