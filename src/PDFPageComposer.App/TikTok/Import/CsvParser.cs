using System.IO;
using System.Text;

namespace PDFPageComposer.App.TikTok.Import;

/// <summary>
/// Minimal streaming RFC 4180 reader: comma separator, quoted fields with "" escapes that may contain commas and
/// line breaks, CRLF or LF line endings. A leading UTF-8 BOM is skipped and a trailing empty line yields no record.
/// </summary>
public static class CsvParser
{
    private const char Separator = ',';
    private const char Quote = '"';
    private const char ByteOrderMark = '﻿';

    public static IEnumerable<IReadOnlyList<string>> ReadRecords(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return ReadRecordsIterator(reader);
    }

    private static IEnumerable<IReadOnlyList<string>> ReadRecordsIterator(TextReader reader)
    {
        if (reader.Peek() == ByteOrderMark)
        {
            reader.Read();
        }

        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var recordHasContent = false;

        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                if (inQuotes)
                {
                    throw new OrderSourceFormatException("File CSV bị lỗi: có dấu ngoặc kép (\") chưa được đóng.");
                }

                if (recordHasContent)
                {
                    fields.Add(field.ToString());
                    yield return fields.ToArray();
                }

                yield break;
            }

            var character = (char)next;
            if (inQuotes)
            {
                if (character != Quote)
                {
                    field.Append(character);
                }
                else if (reader.Peek() == Quote)
                {
                    reader.Read();
                    field.Append(Quote);
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            switch (character)
            {
                case Quote when field.Length == 0:
                    inQuotes = true;
                    recordHasContent = true;
                    break;
                case Separator:
                    fields.Add(field.ToString());
                    field.Clear();
                    recordHasContent = true;
                    break;
                case '\r' or '\n':
                    if (character == '\r' && reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    fields.Add(field.ToString());
                    yield return fields.ToArray();
                    fields.Clear();
                    field.Clear();
                    recordHasContent = false;
                    break;
                default:
                    field.Append(character);
                    recordHasContent = true;
                    break;
            }
        }
    }
}
