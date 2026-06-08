using ChDb.Engine;

namespace ChDb;

/// <summary>
/// Static helper class for one-shot chDB queries without managing a connection.
/// </summary>
public static class ChDbEngine
{
    /// <summary>
    /// Executes a query using command-line style arguments.
    /// This creates a temporary session for each call.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments (e.g., "--query=SELECT 1", "--output-format=CSV").
    /// By convention the first argument is the program name (e.g. "chdb").
    /// </param>
    /// <returns>Query result.</returns>
    public static ChDbResult QueryCommandLine(params string[] args)
    {
        MaterializedQueryResult result = Chdb.QueryCmdline(args);
        return new ChDbResult(result);
    }

    /// <summary>
    /// Returns the library version string.
    /// </summary>
    public static string Version => Chdb.Version;
}
