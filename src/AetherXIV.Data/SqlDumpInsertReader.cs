using System.Text;

namespace AetherXIV.Data;

internal sealed record SqlDumpInsertRow(string SourceRef, IReadOnlyList<string?> Values);

internal static class SqlDumpInsertReader
{
    public static async IAsyncEnumerable<SqlDumpInsertRow> ReadRowsAsync(
        string path,
        string tableName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using StreamReader reader = File.OpenText(path);
        StringBuilder? statement = null;
        int statementStartLine = 0;
        int lineNumber = 0;
        string marker = $"INSERT INTO `{tableName}`";

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (statement is null)
            {
                int insertIndex = line.IndexOf(marker, StringComparison.Ordinal);
                if (insertIndex < 0)
                    continue;

                statementStartLine = lineNumber;
                statement = new StringBuilder(line[insertIndex..]);
            }
            else
            {
                statement.AppendLine();
                statement.Append(line.Trim());
            }

            if (!line.TrimEnd().EndsWith(';'))
                continue;

            int rowIndex = 0;
            foreach (IReadOnlyList<string?> values in ParseStatement(statement.ToString()))
            {
                rowIndex++;
                yield return new SqlDumpInsertRow($"{tableName}:{statementStartLine}:{rowIndex}", values);
            }

            statement = null;
        }
    }

    private static IEnumerable<IReadOnlyList<string?>> ParseStatement(string statement)
    {
        int valuesIndex = statement.IndexOf("VALUES", StringComparison.OrdinalIgnoreCase);
        if (valuesIndex < 0)
            yield break;

        string valuesText = statement[(valuesIndex + "VALUES".Length)..].Trim();
        if (valuesText.EndsWith(';'))
            valuesText = valuesText[..^1].TrimEnd();

        List<string?> currentRow = [];
        StringBuilder currentValue = new();
        bool inString = false;
        bool escaped = false;
        bool inRow = false;
        int depth = 0;

        for (int i = 0; i < valuesText.Length; i++)
        {
            char c = valuesText[i];
            if (inString)
            {
                if (escaped)
                {
                    currentValue.Append(Unescape(c));
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '\'')
                {
                    inString = false;
                    continue;
                }

                currentValue.Append(c);
                continue;
            }

            if (c == '\'')
            {
                inString = true;
                continue;
            }

            if (!inRow)
            {
                if (c == '(')
                {
                    inRow = true;
                    depth = 1;
                    currentRow = [];
                    currentValue.Clear();
                }

                continue;
            }

            if (c == '(')
            {
                depth++;
                currentValue.Append(c);
                continue;
            }

            if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    currentRow.Add(NormalizeValue(currentValue.ToString()));
                    currentValue.Clear();
                    inRow = false;
                    yield return currentRow.ToArray();
                    continue;
                }

                currentValue.Append(c);
                continue;
            }

            if (c == ',' && depth == 1)
            {
                currentRow.Add(NormalizeValue(currentValue.ToString()));
                currentValue.Clear();
                continue;
            }

            currentValue.Append(c);
        }
    }

    private static char Unescape(char c)
    {
        return c switch
        {
            'r' => '\r',
            'n' => '\n',
            't' => '\t',
            '0' => '\0',
            _ => c
        };
    }

    private static string? NormalizeValue(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }
}
