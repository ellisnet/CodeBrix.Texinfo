using System.IO;
using System.Text;

namespace CodeBrix.Texinfo2Html.Sources;

/// <summary>
/// A named piece of Texinfo source text with line endings normalized to <c>\n</c>. Sources are
/// created from files (UTF-8 with byte-order-mark detection) or directly from strings.
/// </summary>
internal sealed class TexinfoSourceText
{
    private TexinfoSourceText(string name, string text)
    {
        Name = name;
        Text = text;
    }

    /// <summary>The source name; the file path for file sources, or a caller-chosen label.</summary>
    public string Name { get; }

    /// <summary>The full source text with all line endings normalized to <c>\n</c>.</summary>
    public string Text { get; }

    /// <summary>
    /// Loads a source file as UTF-8 (honoring a UTF-8, UTF-16 or UTF-32 byte order mark when
    /// present) and normalizes its line endings. Invalid byte sequences become replacement
    /// characters rather than errors.
    /// </summary>
    /// <param name="filePath">Path of the file to read.</param>
    public static TexinfoSourceText Load(string filePath)
    {
        using StreamReader reader = new StreamReader(
            filePath,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true);
        return new TexinfoSourceText(Path.GetFullPath(filePath), NormalizeNewlines(reader.ReadToEnd()));
    }

    /// <summary>Wraps an in-memory string as a source, normalizing its line endings.</summary>
    /// <param name="name">Label used in positions and warnings for this source.</param>
    /// <param name="text">The Texinfo source text.</param>
    public static TexinfoSourceText FromString(string name, string text)
        => new TexinfoSourceText(name ?? "(string)", NormalizeNewlines(text ?? string.Empty));

    private static string NormalizeNewlines(string text)
        => text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', '\n') : text;
}
