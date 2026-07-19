using System.Text;

namespace AetherXIV.Launcher.Core;

public static class CommandLineArguments
{
    public static string Quote(string value)
    {
        if (value.Length == 0)
            return "\"\"";

        bool needsQuotes = value.Any(char.IsWhiteSpace) || value.Contains('"');
        if (!needsQuotes)
            return value;

        StringBuilder builder = new();
        builder.Append('"');
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(character);
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
