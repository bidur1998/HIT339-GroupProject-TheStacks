using System.Text;

namespace LibraryManagementSystem.Services;

/// <summary>
/// A small CSV reader written for the importer (RFC 4180 style): handles
/// commas and line breaks inside "quoted" fields and "" as an escaped
/// quote. Written by hand rather than adding a NuGet package, since the
/// import format is simple and this keeps the dependency list short.
/// </summary>
public static class CsvParser
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        // Strip a UTF-8 byte order mark that Excel likes to add.
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"'); // escaped quote
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break; // handled with the \n
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    AddRow(rows, fields);
                    fields = new List<string>();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        // Last line may not end with a newline.
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            AddRow(rows, fields);
        }

        return rows;
    }

    private static void AddRow(List<string[]> rows, List<string> fields)
    {
        // Skip completely blank lines.
        if (fields.All(string.IsNullOrWhiteSpace))
        {
            return;
        }
        rows.Add(fields.Select(f => f.Trim()).ToArray());
    }
}
