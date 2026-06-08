# chdbsharp

A pure **C#** port of [chdbwin](https://github.com/) — the Windows VC++ build of
chDB (an embedded ClickHouse-style SQL engine). The original implements its query
engine in native C++ exposed through a `libchdb.dll` C API and consumed from .NET
via P/Invoke. **chdbsharp reimplements that engine entirely in managed C#**, so
there is no native dependency: the library is fully cross-platform and runs
anywhere .NET 8 runs.

## What was translated

| C++ source (`chdbwin`)            | C# equivalent (`chdbsharp`)                       |
| --------------------------------- | ------------------------------------------------- |
| `src/engine/EmbeddedEngine.*`     | `src/ChDb.Net/Engine/EmbeddedEngine.cs`           |
| `src/chdb/QueryResult.h`          | `src/ChDb.Net/Engine/QueryResult.cs`              |
| structs in `EmbeddedEngine.h`     | `Engine/ColumnValue.cs`, `Engine/Schema.cs`       |
| `src/chdb/chdb.cpp` (C API)       | `src/ChDb.Net/Engine/Chdb.cs`                     |
| `dotnet/ChDb.Net/*` (P/Invoke)    | public API in `src/ChDb.Net/*.cs` (now managed)   |

The previous C# layer was a thin P/Invoke wrapper around the native DLL. The public
API surface (`ChDbConnection`, `ChDbResult`, `ChDbEngine`, `ChDbException`) is kept
identical, but it now calls straight into the managed engine instead of marshalling
across the native boundary — so the original test suite passes unchanged.

## Project layout

```
chdbsharp/
├── ChDbSharp.slnx
├── src/ChDb.Net/
│   ├── ChDbConnection.cs      # public: open a session, run queries
│   ├── ChDbResult.cs          # public: result buffer + statistics
│   ├── ChDbEngine.cs          # public: one-shot command-line queries + version
│   ├── ChDbException.cs       # public: error type
│   └── Engine/                # internal managed engine (the translated C++)
│       ├── Chdb.cs            # connection management & query dispatch (chdb.cpp)
│       ├── EmbeddedEngine.cs  # the in-memory SQL engine
│       ├── QueryResult.cs     # materialized/stream result hierarchy
│       ├── ColumnValue.cs     # a single typed cell value
│       └── Schema.cs          # ColumnDef / TableDef / ResultSet
└── tests/ChDb.Net.Tests/      # MSTest suite (ported + additional engine tests)
```

## Usage

```csharp
using ChDb;

using var conn = new ChDbConnection(":memory:");

conn.QueryString("CREATE TABLE users (id Int64, name String, age Int64)");
conn.QueryString("INSERT INTO users VALUES (1, 'Alice', 30), (2, 'Bob', 25)");

string csv = conn.QueryString("SELECT * FROM users WHERE age > 26", "CSV");
// 1,"Alice",30

string json = conn.QueryString("SELECT 42 AS answer", "JSON");
// {"answer":42}

// One-shot query without managing a connection:
using var r = ChDbEngine.QueryCommandLine("chdb", "--query=SELECT 1", "--output-format=CSV");
Console.WriteLine(r.GetString()); // 1
```

### Supported SQL

This is a deliberately small engine mirroring the C++ original, not full
ClickHouse. It supports:

- `CREATE TABLE [IF NOT EXISTS] name (col Type, ...)`
- `INSERT INTO name [(cols)] VALUES (...), (...)` (missing trailing values padded with NULL)
- `SELECT <exprs|*> [FROM table] [WHERE col OP value]` with `= != > < >= <=`
- `DROP TABLE [IF EXISTS] name`
- `SHOW TABLES`, `DESCRIBE` / `DESC name`
- Literals, integer/float arithmetic (`+ - * /`), `AS` aliases, and a few
  functions (`now()`, `version()`, `database()`)
- Output formats: `CSV`, `TSV`/`TabSeparated`, `JSON`/`JSONEachRow`, `Pretty`

> The engine is a process-wide singleton (matching the C++ design), so tables
> created on one connection are visible to others in the same process.

## Build & test

```sh
dotnet build ChDbSharp.slnx
dotnet test  ChDbSharp.slnx
```

Targets `net8.0`. No native libraries required.
