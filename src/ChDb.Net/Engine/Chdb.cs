using System;

namespace ChDb.Engine;

/// <summary>
/// Internal connection state. Port of the C++ <c>chdb_conn</c> struct.
/// </summary>
internal sealed class ChdbConn
{
    public EmbeddedEngine? Engine;
    public bool Connected;
}

/// <summary>
/// Managed translation of the chDB C API (chdb.cpp): connection management and
/// query dispatch over the <see cref="EmbeddedEngine"/> singleton.
/// Errors are surfaced as <see cref="MaterializedQueryResult"/> values carrying an
/// error message rather than thrown, matching the C API contract.
/// </summary>
internal static class Chdb
{
    public const string Version = "1.0.0";

    private static readonly object ConnectionLock = new();
    private static ChdbConn? _activeConnection;

    private static bool CheckConnectionValidity(ChdbConn? conn) =>
        conn is { Connected: true, Engine: not null };

    /// <summary>Parse the <c>--path</c> argument. Mirrors <c>parsePathFromArgs</c>.</summary>
    private static string ParsePathFromArgs(string[] argv)
    {
        for (int i = 1; i < argv.Length; i++)
        {
            string arg = argv[i];
            if (arg.StartsWith("--path=", StringComparison.Ordinal))
                return arg.Substring(7);
            if (arg == "--path" && i + 1 < argv.Length)
                return argv[i + 1];
        }
        return ":memory:";
    }

    // ============== Connection API ==============

    /// <summary>Mirrors <c>chdb_connect</c>. Returns null on failure.</summary>
    public static ChdbConn? Connect(string[] argv)
    {
        lock (ConnectionLock)
        {
            try
            {
                string path = ParsePathFromArgs(argv);

                EmbeddedEngine engine = EmbeddedEngine.Instance;
                if (!engine.IsInitialized)
                    engine.Initialize(path);

                var conn = new ChdbConn { Engine = engine, Connected = true };
                _activeConnection = conn;
                return conn;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Mirrors <c>chdb_close_conn</c>.</summary>
    public static void CloseConn(ChdbConn? conn)
    {
        if (conn == null)
            return;

        lock (ConnectionLock)
        {
            conn.Connected = false;
            if (ReferenceEquals(conn, _activeConnection))
                _activeConnection = null;
        }
    }

    // ============== Query API ==============

    /// <summary>Mirrors <c>chdb_query</c> / <c>chdb_query_n</c>.</summary>
    public static MaterializedQueryResult Query(ChdbConn? conn, string? query, string? format)
    {
        if (conn == null)
            return new MaterializedQueryResult("Unexpected null connection");

        if (!CheckConnectionValidity(conn))
            return new MaterializedQueryResult("Invalid or closed connection");

        try
        {
            string queryStr = query ?? string.Empty;
            // The C API defaults a null format to CSV; an empty format is also
            // treated as CSV downstream by the formatter.
            string formatStr = format ?? "CSV";

            return conn.Engine!.ExecuteQuery(queryStr, formatStr);
        }
        catch (Exception e)
        {
            return new MaterializedQueryResult("Error: " + e.Message);
        }
    }

    /// <summary>Mirrors <c>chdb_query_cmdline</c>.</summary>
    public static MaterializedQueryResult QueryCmdline(string[] argv)
    {
        string query = string.Empty;
        string format = "CSV";

        for (int i = 1; i < argv.Length; i++)
        {
            string arg = argv[i];
            if (arg.StartsWith("--query=", StringComparison.Ordinal) ||
                arg.StartsWith("-q=", StringComparison.Ordinal))
            {
                int eq = arg.IndexOf('=');
                query = arg.Substring(eq + 1);
            }
            else if ((arg == "--query" || arg == "-q") && i + 1 < argv.Length)
            {
                query = argv[++i];
            }
            else if (arg.StartsWith("--output-format=", StringComparison.Ordinal))
            {
                format = arg.Substring(16);
            }
            else if (arg == "--output-format" && i + 1 < argv.Length)
            {
                format = argv[++i];
            }
            else if (arg.StartsWith("-f=", StringComparison.Ordinal))
            {
                format = arg.Substring(3);
            }
        }

        if (query.Length == 0)
            return new MaterializedQueryResult("No query specified");

        EmbeddedEngine engine = EmbeddedEngine.Instance;
        if (!engine.IsInitialized)
        {
            string path = ParsePathFromArgs(argv);
            engine.Initialize(path);
        }

        try
        {
            return engine.ExecuteQuery(query, format);
        }
        catch (Exception e)
        {
            return new MaterializedQueryResult("Error: " + e.Message);
        }
    }
}
