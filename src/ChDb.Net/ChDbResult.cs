using System;
using System.Text;
using ChDb.Engine;

namespace ChDb;

/// <summary>
/// Represents the result of a chDB query.
/// Wraps a materialized engine result and provides safe access to result data.
/// </summary>
public sealed class ChDbResult : IDisposable
{
    private MaterializedQueryResult? _result;
    private bool _disposed;

    internal ChDbResult(MaterializedQueryResult result)
    {
        _result = result ?? throw new ChDbException("Query returned null result handle");

        // Capture the error message immediately so it survives disposal.
        ErrorMessage = result.ErrorMessage.Length > 0 ? result.ErrorMessage : null;
    }

    /// <summary>
    /// Gets the error message, if any. Null if query succeeded.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Returns true if the query produced an error.
    /// </summary>
    public bool HasError => ErrorMessage != null;

    /// <summary>
    /// Throws a <see cref="ChDbException"/> if the result contains an error.
    /// </summary>
    public ChDbResult ThrowIfError()
    {
        if (HasError)
            throw new ChDbException(ErrorMessage!);
        return this;
    }

    /// <summary>
    /// Gets the raw result data as a byte span.
    /// </summary>
    public ReadOnlySpan<byte> GetBuffer()
    {
        ThrowIfDisposed();
        byte[]? buffer = _result!.ResultBuffer;
        if (buffer == null || buffer.Length == 0)
            return ReadOnlySpan<byte>.Empty;
        return buffer;
    }

    /// <summary>
    /// Gets the result data as a UTF-8 string.
    /// </summary>
    public string GetString()
    {
        ReadOnlySpan<byte> buffer = GetBuffer();
        if (buffer.IsEmpty)
            return string.Empty;
        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Gets the length of the result data in bytes.
    /// </summary>
    public long Length
    {
        get
        {
            ThrowIfDisposed();
            return _result!.ResultBuffer?.Length ?? 0;
        }
    }

    /// <summary>
    /// Gets the query execution elapsed time in seconds.
    /// </summary>
    public double Elapsed
    {
        get
        {
            ThrowIfDisposed();
            return _result!.Elapsed;
        }
    }

    /// <summary>
    /// Gets the number of rows in the result.
    /// </summary>
    public ulong RowsRead
    {
        get
        {
            ThrowIfDisposed();
            return _result!.RowsRead;
        }
    }

    /// <summary>
    /// Gets the number of bytes in the result.
    /// </summary>
    public ulong BytesRead
    {
        get
        {
            ThrowIfDisposed();
            return _result!.BytesRead;
        }
    }

    /// <summary>
    /// Gets the number of rows read from storage.
    /// </summary>
    public ulong StorageRowsRead
    {
        get
        {
            ThrowIfDisposed();
            return _result!.StorageRowsRead;
        }
    }

    /// <summary>
    /// Gets the number of bytes read from storage.
    /// </summary>
    public ulong StorageBytesRead
    {
        get
        {
            ThrowIfDisposed();
            return _result!.StorageBytesRead;
        }
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
            _result = null;
        }
    }
}
