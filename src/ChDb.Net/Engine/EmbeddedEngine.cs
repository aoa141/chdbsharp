using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChDb.Engine;

/// <summary>
/// The embedded SQL engine — provides basic in-memory SQL support for chDB.
/// Pure-managed port of the C++ <c>ChDB::EmbeddedEngine</c> class
/// (EmbeddedEngine.h / EmbeddedEngine.cpp).
/// </summary>
internal sealed class EmbeddedEngine
{
    private static readonly object InstanceLock = new();
    private static EmbeddedEngine? _instance;

    private readonly object _engineLock = new();
    private readonly Dictionary<string, TableDef> _tables = new();
    private string _dbPath = string.Empty;

    private static readonly Regex AliasRegex =
        new(@"^(.+?)\s+[Aa][Ss]\s+(\w+)$", RegexOptions.Compiled);

    private EmbeddedEngine()
    {
    }

    /// <summary>Process-wide singleton instance. Mirrors <c>getInstance()</c>.</summary>
    public static EmbeddedEngine Instance
    {
        get
        {
            lock (InstanceLock)
            {
                return _instance ??= new EmbeddedEngine();
            }
        }
    }

    /// <summary>Releases the singleton. Mirrors <c>releaseInstance()</c>.</summary>
    public static void ReleaseInstance()
    {
        lock (InstanceLock)
        {
            _instance = null;
        }
    }

    public bool IsInitialized { get; private set; }

    public string Path => _dbPath;

    /// <summary>Initialize with a database path.</summary>
    public void Initialize(string path)
    {
        lock (_engineLock)
        {
            _dbPath = path;
            IsInitialized = true;
        }
    }

    // ============== Query execution ==============

    /// <summary>
    /// Execute a SQL query and return a materialized result.
    /// Port of <c>EmbeddedEngine::executeQuery</c>.
    /// </summary>
    public MaterializedQueryResult ExecuteQuery(string query, string format)
    {
        lock (_engineLock)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                string trimmed = Trim(query);
                // Remove trailing semicolon
                if (trimmed.Length > 0 && trimmed[^1] == ';')
                    trimmed = trimmed.Substring(0, trimmed.Length - 1);
                trimmed = Trim(trimmed);

                string upper = ToUpper(trimmed);

                ResultSet rs;

                if (StartsWith(upper, "SELECT"))
                {
                    rs = ExecuteSelect(trimmed);
                }
                else if (StartsWith(upper, "CREATE TABLE"))
                {
                    if (ExecuteCreateTable(trimmed))
                        return EmptyResult(stopwatch);
                    return new MaterializedQueryResult("Failed to create table");
                }
                else if (StartsWith(upper, "INSERT INTO"))
                {
                    if (ExecuteInsert(trimmed))
                        return EmptyResult(stopwatch);
                    return new MaterializedQueryResult("Failed to insert data");
                }
                else if (StartsWith(upper, "DROP TABLE"))
                {
                    if (ExecuteDropTable(trimmed))
                        return EmptyResult(stopwatch);
                    return new MaterializedQueryResult("Failed to drop table");
                }
                else if (upper == "SHOW TABLES" || upper == "SHOW TABLES;")
                {
                    rs = ExecuteShowTables();
                }
                else if (StartsWith(upper, "DESCRIBE") || StartsWith(upper, "DESC"))
                {
                    string tableName = StartsWith(upper, "DESCRIBE")
                        ? Trim(trimmed.Substring(8))
                        : Trim(trimmed.Substring(4));
                    rs = ExecuteDescribe(tableName);
                }
                else
                {
                    return new MaterializedQueryResult("Unsupported query: " + trimmed);
                }

                double elapsed = stopwatch.Elapsed.TotalSeconds;
                string output = FormatResult(rs, format);

                byte[] buf = Encoding.UTF8.GetBytes(output);
                ulong rows = (ulong)rs.Rows.Count;
                ulong bytes = (ulong)buf.Length;

                return new MaterializedQueryResult(buf, elapsed, rows, bytes, rows, bytes);
            }
            catch (Exception e)
            {
                return new MaterializedQueryResult("Error: " + e.Message);
            }
        }
    }

    private static MaterializedQueryResult EmptyResult(Stopwatch stopwatch)
    {
        double elapsed = stopwatch.Elapsed.TotalSeconds;
        return new MaterializedQueryResult(Array.Empty<byte>(), elapsed, 0, 0, 0, 0);
    }

    // ============== SELECT ==============

    private ResultSet ExecuteSelect(string query)
    {
        var rs = new ResultSet();

        // Find FROM clause position (if any) by searching the uppercased copy.
        string upperQ = ToUpper(query);
        int fromPos = upperQ.IndexOf(" FROM ", StringComparison.Ordinal);

        // Extract the select expression list (skip "SELECT").
        string selectExprStr = fromPos >= 0
            ? query.Substring(6, fromPos - 6)
            : query.Substring(6);
        selectExprStr = Trim(selectExprStr);

        // If there's a FROM clause, gather the table contents.
        ResultSet? tableContext = null;
        string whereClause = string.Empty;

        if (fromPos >= 0)
        {
            string rest = Trim(query.Substring(fromPos + 6));
            string upperRest = ToUpper(rest);

            // Extract table name (stop at WHERE/ORDER/LIMIT/GROUP/HAVING).
            int endPos = rest.Length;
            foreach (string kw in new[] { "WHERE ", "ORDER ", "LIMIT ", "GROUP ", "HAVING " })
            {
                int p = upperRest.IndexOf(kw, StringComparison.Ordinal);
                if (p >= 0 && p < endPos)
                    endPos = p;
            }
            string tableName = Trim(rest.Substring(0, endPos));

            // Extract WHERE clause if present.
            int wherePos = upperRest.IndexOf("WHERE ", StringComparison.Ordinal);
            if (wherePos >= 0)
            {
                string afterWhere = rest.Substring(wherePos + 6);
                string upperAw = ToUpper(afterWhere);
                int wend = afterWhere.Length;
                foreach (string kw in new[] { "ORDER ", "LIMIT ", "GROUP " })
                {
                    int p = upperAw.IndexOf(kw, StringComparison.Ordinal);
                    if (p >= 0 && p < wend)
                        wend = p;
                }
                whereClause = Trim(afterWhere.Substring(0, wend));
            }

            if (!_tables.TryGetValue(tableName, out TableDef? tbl))
                throw new InvalidOperationException("Table not found: " + tableName);

            var tableData = new ResultSet();
            foreach (ColumnDef col in tbl.Columns)
                tableData.ColumnNames.Add(col.Name);
            foreach (List<ColumnValue> row in tbl.Rows)
                tableData.Rows.Add(new List<ColumnValue>(row));
            tableContext = tableData;
        }

        // Parse select expressions.
        List<string> expressions = SplitComma(selectExprStr);

        // Handle SELECT *
        if (expressions.Count == 1 && Trim(expressions[0]) == "*" && tableContext != null)
        {
            rs.ColumnNames.AddRange(tableContext.ColumnNames);
            if (whereClause.Length == 0)
            {
                foreach (List<ColumnValue> row in tableContext.Rows)
                    rs.Rows.Add(new List<ColumnValue>(row));
            }
            else
            {
                for (int rowIdx = 0; rowIdx < tableContext.Rows.Count; rowIdx++)
                {
                    if (EvaluateWhere(whereClause, tableContext, rowIdx))
                        rs.Rows.Add(new List<ColumnValue>(tableContext.Rows[rowIdx]));
                }
            }
            return rs;
        }

        // Build column names from expressions (honouring AS aliases).
        foreach (string expr in expressions)
        {
            string trimmedExpr = Trim(expr);
            Match match = AliasRegex.Match(trimmedExpr);
            if (match.Success)
                rs.ColumnNames.Add(match.Groups[2].Value);
            else
                rs.ColumnNames.Add(trimmedExpr);
        }

        if (tableContext != null && tableContext.Rows.Count > 0)
        {
            for (int rowIdx = 0; rowIdx < tableContext.Rows.Count; rowIdx++)
            {
                if (whereClause.Length > 0 && !EvaluateWhere(whereClause, tableContext, rowIdx))
                    continue;

                var row = new List<ColumnValue>();
                foreach (string expr in expressions)
                    row.Add(EvaluateExpression(expr, tableContext, rowIdx));
                rs.Rows.Add(row);
            }
        }
        else if (tableContext == null)
        {
            // No FROM clause — evaluate expressions once (e.g. SELECT 1, SELECT 'hello').
            var row = new List<ColumnValue>();
            foreach (string expr in expressions)
                row.Add(EvaluateExpression(expr, null, 0));
            rs.Rows.Add(row);
        }

        return rs;
    }

    private static readonly string[] WhereOperators =
        { " != ", " = ", " >= ", " <= ", " > ", " < " };

    /// <summary>
    /// Evaluates a simple WHERE predicate of the form "left OP right" against a
    /// single row. Mirrors the duplicated operator handling in <c>executeSelect</c>.
    /// </summary>
    private bool EvaluateWhere(string whereClause, ResultSet context, int rowIdx)
    {
        bool passes = true;
        foreach (string op in WhereOperators)
        {
            int opPos = whereClause.IndexOf(op, StringComparison.Ordinal);
            if (opPos < 0)
                continue;

            ColumnValue leftVal = EvaluateExpression(
                Trim(whereClause.Substring(0, opPos)), context, rowIdx);
            ColumnValue rightVal = EvaluateExpression(
                Trim(whereClause.Substring(opPos + op.Length)), context, rowIdx);

            string opStr = Trim(op);

            bool BothInt() =>
                leftVal.Type == ColumnValueType.Int64 && rightVal.Type == ColumnValueType.Int64;

            switch (opStr)
            {
                case "=":
                    passes = leftVal.ToString() == rightVal.ToString();
                    break;
                case "!=":
                    passes = leftVal.ToString() != rightVal.ToString();
                    break;
                case ">":
                    passes = BothInt()
                        ? leftVal.I64 > rightVal.I64
                        : string.CompareOrdinal(leftVal.ToString(), rightVal.ToString()) > 0;
                    break;
                case "<":
                    passes = BothInt()
                        ? leftVal.I64 < rightVal.I64
                        : string.CompareOrdinal(leftVal.ToString(), rightVal.ToString()) < 0;
                    break;
                case ">=":
                    passes = BothInt()
                        ? leftVal.I64 >= rightVal.I64
                        : string.CompareOrdinal(leftVal.ToString(), rightVal.ToString()) >= 0;
                    break;
                case "<=":
                    passes = BothInt()
                        ? leftVal.I64 <= rightVal.I64
                        : string.CompareOrdinal(leftVal.ToString(), rightVal.ToString()) <= 0;
                    break;
            }
            break;
        }
        return passes;
    }

    // ============== CREATE / INSERT / DROP / SHOW / DESCRIBE ==============

    private bool ExecuteCreateTable(string query)
    {
        // CREATE TABLE [IF NOT EXISTS] name (col1 Type1, col2 Type2, ...)
        string rest = Trim(query.Substring(12)); // skip "CREATE TABLE"
        string upper = ToUpper(rest);

        if (StartsWith(upper, "IF NOT EXISTS"))
        {
            rest = Trim(rest.Substring(13));
        }

        int parenPos = rest.IndexOf('(');
        if (parenPos < 0)
            return false;

        string tableName = Trim(rest.Substring(0, parenPos));

        int closeParen = rest.LastIndexOf(')');
        if (closeParen < 0 || closeParen <= parenPos)
            return false;

        string colsStr = rest.Substring(parenPos + 1, closeParen - parenPos - 1);
        List<string> colDefs = SplitComma(colsStr);

        var table = new TableDef { Name = tableName };

        foreach (string colDef in colDefs)
        {
            string trimmed = Trim(colDef);
            int spacePos = trimmed.IndexOf(' ');
            if (spacePos < 0)
                continue;

            table.Columns.Add(new ColumnDef
            {
                Name = Trim(trimmed.Substring(0, spacePos)),
                Type = Trim(trimmed.Substring(spacePos + 1)),
            });
        }

        _tables[tableName] = table;
        return true;
    }

    private bool ExecuteInsert(string query)
    {
        // INSERT INTO table_name [(cols)] VALUES (v1, v2, ...), (...)
        string rest = Trim(query.Substring(11)); // skip "INSERT INTO"

        int spaceOrParen = rest.IndexOfAny(new[] { ' ', '(' });
        if (spaceOrParen < 0)
            return false;

        string tableName = Trim(rest.Substring(0, spaceOrParen));
        if (!_tables.TryGetValue(tableName, out TableDef? table))
            return false;

        string upperRest = ToUpper(rest);
        int valuesPos = upperRest.IndexOf("VALUES", StringComparison.Ordinal);
        if (valuesPos < 0)
            return false;

        string valuesStr = rest.Substring(valuesPos + 6);

        // Parse value tuples: (v1, v2), (v3, v4)
        int pos = 0;
        while (pos < valuesStr.Length)
        {
            int open = valuesStr.IndexOf('(', pos);
            if (open < 0)
                break;

            int close = valuesStr.IndexOf(')', open);
            if (close < 0)
                break;

            string tuple = valuesStr.Substring(open + 1, close - open - 1);
            List<string> vals = SplitComma(tuple);

            var row = new List<ColumnValue>();
            foreach (string v in vals)
                row.Add(EvaluateExpression(v, null, 0));

            // Pad with NULLs if needed.
            while (row.Count < table.Columns.Count)
                row.Add(ColumnValue.Null);

            table.Rows.Add(row);
            pos = close + 1;
        }

        return true;
    }

    private bool ExecuteDropTable(string query)
    {
        string rest = Trim(query.Substring(10)); // skip "DROP TABLE"
        string upper = ToUpper(rest);

        bool ifExists = false;
        if (StartsWith(upper, "IF EXISTS"))
        {
            ifExists = true;
            rest = Trim(rest.Substring(9));
        }

        string tableName = Trim(rest);
        if (!_tables.ContainsKey(tableName))
            return ifExists; // not found

        _tables.Remove(tableName);
        return true;
    }

    private ResultSet ExecuteShowTables()
    {
        var rs = new ResultSet();
        rs.ColumnNames.Add("name");
        foreach (KeyValuePair<string, TableDef> pair in _tables)
        {
            rs.Rows.Add(new List<ColumnValue> { new ColumnValue(pair.Key) });
        }
        return rs;
    }

    private ResultSet ExecuteDescribe(string tableName)
    {
        string name = Trim(tableName);
        if (!_tables.TryGetValue(name, out TableDef? table))
            throw new InvalidOperationException("Table not found: " + name);

        var rs = new ResultSet();
        rs.ColumnNames.Add("name");
        rs.ColumnNames.Add("type");
        foreach (ColumnDef col in table.Columns)
        {
            rs.Rows.Add(new List<ColumnValue>
            {
                new ColumnValue(col.Name),
                new ColumnValue(col.Type),
            });
        }
        return rs;
    }

    // ============== Expression evaluation ==============

    /// <summary>
    /// Evaluates a simple expression (literal, column reference, arithmetic, or
    /// a small set of functions). Port of <c>EmbeddedEngine::evaluateExpression</c>.
    /// </summary>
    private ColumnValue EvaluateExpression(string expr, ResultSet? context, int rowIdx)
    {
        string trimmed = Trim(expr);

        // String literal
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
            return new ColumnValue(trimmed.Substring(1, trimmed.Length - 2));

        // Arithmetic: check for + - * / operators BEFORE numeric literals so that
        // "10 + 20" is handled as arithmetic, not as numeric "10".
        foreach (string op in new[] { " + ", " - ", " * ", " / " })
        {
            int pos = trimmed.IndexOf(op, StringComparison.Ordinal);
            if (pos < 0)
                continue;

            ColumnValue left = EvaluateExpression(trimmed.Substring(0, pos), context, rowIdx);
            ColumnValue right = EvaluateExpression(trimmed.Substring(pos + 3), context, rowIdx);
            char opChar = op[1];

            if (left.Type == ColumnValueType.Int64 && right.Type == ColumnValueType.Int64)
            {
                return opChar switch
                {
                    '+' => new ColumnValue(left.I64 + right.I64),
                    '-' => new ColumnValue(left.I64 - right.I64),
                    '*' => new ColumnValue(left.I64 * right.I64),
                    '/' => right.I64 != 0 ? new ColumnValue(left.I64 / right.I64) : ColumnValue.Null,
                    _ => ColumnValue.Null,
                };
            }
            else
            {
                double l = left.Type == ColumnValueType.Float64 ? left.F64 : left.I64;
                double r = right.Type == ColumnValueType.Float64 ? right.F64 : right.I64;
                return opChar switch
                {
                    '+' => new ColumnValue(l + r),
                    '-' => new ColumnValue(l - r),
                    '*' => new ColumnValue(l * r),
                    '/' => r != 0.0 ? new ColumnValue(l / r) : ColumnValue.Null,
                    _ => ColumnValue.Null,
                };
            }
        }

        // Numeric literal
        if (trimmed.Length > 0 && (char.IsDigit(trimmed[0]) || trimmed[0] == '-'))
        {
            bool hasDot = trimmed.IndexOf('.') >= 0;
            if (hasDot)
            {
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    return new ColumnValue(d);
            }
            else
            {
                if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    return new ColumnValue(l);
            }
        }

        // NULL
        if (ToUpper(trimmed) == "NULL")
            return ColumnValue.Null;

        // Simple functions
        string upper = ToUpper(trimmed);

        if (upper == "NOW()" || upper == "CURRENT_TIMESTAMP()")
            return new ColumnValue("2026-05-01 00:00:00");

        if (StartsWith(upper, "VERSION(") || upper == "VERSION()")
            return new ColumnValue("chDB Windows 1.0.0");

        if (upper == "DATABASE()" || upper == "CURRENTDATABASE()")
            return new ColumnValue(_dbPath.Length == 0 ? ":memory:" : _dbPath);

        // Column reference in context
        if (context != null && context.ColumnNames.Count > 0)
        {
            for (int i = 0; i < context.ColumnNames.Count; i++)
            {
                if (context.ColumnNames[i] == trimmed && rowIdx < context.Rows.Count)
                    return context.Rows[rowIdx][i];
            }
        }

        // Alias handling: "expr AS name" — evaluate the expression part.
        Match match = AliasRegex.Match(trimmed);
        if (match.Success)
            return EvaluateExpression(match.Groups[1].Value, context, rowIdx);

        // Unknown — return as string.
        return new ColumnValue(trimmed);
    }

    // ============== Formatting ==============

    private string FormatResult(ResultSet rs, string format)
    {
        string fmt = ToUpper(Trim(format));

        if (fmt.Length == 0 || fmt == "CSV")
            return FormatCsv(rs);
        if (fmt == "TSV" || fmt == "TABSEPARATED")
            return FormatTsv(rs);
        if (fmt == "JSON" || fmt == "JSONEACHROW")
            return FormatJson(rs);
        if (fmt == "PRETTY")
            return FormatPretty(rs);

        return FormatCsv(rs);
    }

    private static string FormatCsv(ResultSet rs)
    {
        var sb = new StringBuilder();
        foreach (List<ColumnValue> row in rs.Rows)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (i > 0) sb.Append(',');
                if (row[i].Type == ColumnValueType.String)
                    sb.Append('"').Append(row[i].Str).Append('"');
                else
                    sb.Append(row[i].ToString());
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string FormatTsv(ResultSet rs)
    {
        var sb = new StringBuilder();
        foreach (List<ColumnValue> row in rs.Rows)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (i > 0) sb.Append('\t');
                sb.Append(row[i].ToString());
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string FormatJson(ResultSet rs)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < rs.Rows.Count; r++)
        {
            sb.Append('{');
            for (int c = 0; c < rs.ColumnNames.Count && c < rs.Rows[r].Count; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append('"').Append(rs.ColumnNames[c]).Append("\":");

                ColumnValue val = rs.Rows[r][c];
                if (val.Type == ColumnValueType.String)
                    sb.Append('"').Append(val.Str).Append('"');
                else if (val.Type == ColumnValueType.Null)
                    sb.Append("null");
                else
                    sb.Append(val.ToString());
            }
            sb.Append("}\n");
        }
        return sb.ToString();
    }

    private static string FormatPretty(ResultSet rs)
    {
        if (rs.ColumnNames.Count == 0)
            return string.Empty;

        // Calculate column widths.
        var widths = new int[rs.ColumnNames.Count];
        for (int i = 0; i < rs.ColumnNames.Count; i++)
            widths[i] = rs.ColumnNames[i].Length;

        foreach (List<ColumnValue> row in rs.Rows)
        {
            for (int i = 0; i < row.Count && i < widths.Length; i++)
            {
                int len = row[i].ToString().Length;
                if (len > widths[i])
                    widths[i] = len;
            }
        }

        var sb = new StringBuilder();

        void Separator()
        {
            sb.Append('+');
            foreach (int w in widths)
                sb.Append(new string('-', w + 2)).Append('+');
            sb.Append('\n');
        }

        Separator();

        // Header
        sb.Append('|');
        for (int i = 0; i < rs.ColumnNames.Count; i++)
        {
            sb.Append(' ');
            sb.Append(rs.ColumnNames[i]);
            sb.Append(new string(' ', widths[i] - rs.ColumnNames[i].Length + 1)).Append('|');
        }
        sb.Append('\n');

        Separator();

        // Rows
        foreach (List<ColumnValue> row in rs.Rows)
        {
            sb.Append('|');
            for (int i = 0; i < row.Count && i < widths.Length; i++)
            {
                string val = row[i].ToString();
                sb.Append(' ').Append(val).Append(new string(' ', widths[i] - val.Length + 1)).Append('|');
            }
            sb.Append('\n');
        }

        Separator();

        return sb.ToString();
    }

    // ============== Helpers ==============

    private static readonly char[] TrimChars = { ' ', '\t', '\r', '\n' };

    /// <summary>Trim whitespace. Mirrors <c>EmbeddedEngine::trim</c>.</summary>
    private static string Trim(string s) => s.Trim(TrimChars);

    /// <summary>Uppercase (ASCII). Mirrors <c>EmbeddedEngine::toUpper</c>.</summary>
    private static string ToUpper(string s) => s.ToUpperInvariant();

    private static bool StartsWith(string s, string prefix) =>
        s.StartsWith(prefix, StringComparison.Ordinal);

    /// <summary>
    /// Splits a comma-separated list, respecting parenthesis depth.
    /// Mirrors <c>EmbeddedEngine::splitComma</c>.
    /// </summary>
    private static List<string> SplitComma(string s)
    {
        var parts = new List<string>();
        int parenDepth = 0;
        var current = new StringBuilder();
        foreach (char c in s)
        {
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == ',' && parenDepth == 0)
            {
                parts.Add(Trim(current.ToString()));
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0)
            parts.Add(Trim(current.ToString()));
        return parts;
    }
}
