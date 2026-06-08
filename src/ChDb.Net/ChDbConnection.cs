using System;
using ChDb.Engine;

namespace ChDb;

/// <summary>
/// Represents a connection to the chDB embedded database engine.
/// Only one active connection per process is supported.
/// </summary>
public sealed class ChDbConnection : IDisposable
{
    private ChdbConn? _conn;
    private bool _disposed;

    /// <summary>
    /// Creates a new chDB connection with the specified database path.
    /// </summary>
    /// <param name="path">Database path. Use ":memory:" for in-memory database.</param>
    public ChDbConnection(string path = ":memory:")
        : this(new[] { "chdb", $"--path={path}" })
    {
    }

    /// <summary>
    /// Creates a new chDB connection with custom arguments.
    /// </summary>
    /// <param name="args">Command-line style arguments for the chDB engine.</param>
    public ChDbConnection(string[] args)
    {
        _conn = Chdb.Connect(args);
        if (_conn == null)
            throw new ChDbException("Failed to create chDB connection");
    }

    /// <summary>
    /// Executes a SQL query and returns the result.
    /// </summary>
    /// <param name="query">SQL query string.</param>
    /// <param name="format">Output format (CSV, TSV, JSON, Pretty). Default: CSV.</param>
    /// <returns>Query result containing the output data.</returns>
    public ChDbResult Query(string query, string format = "CSV")
    {
        ThrowIfDisposed();
        MaterializedQueryResult result = Chdb.Query(_conn, query, format);
        return new ChDbResult(result);
    }

    /// <summary>
    /// Executes a SQL query and returns the result as a string.
    /// Throws on error.
    /// </summary>
    public string QueryString(string query, string format = "CSV")
    {
        using ChDbResult result = Query(query, format);
        result.ThrowIfError();
        return result.GetString();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_conn != null)
            {
                Chdb.CloseConn(_conn);
                _conn = null;
            }
        }
    }
}
