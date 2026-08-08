namespace CodeBrix.Texinfo2Html;

/// <summary>
/// One music environment taken out of a document and handed to an
/// <see cref="ILilypondSnippetRenderer"/> to be engraved. It carries everything the renderer needs
/// and nothing about the document it came from, so a renderer never has to know what Texinfo is.
/// </summary>
/// <remarks>
/// The music source is passed through exactly as the document wrote it. It was captured raw and was
/// never treated as Texinfo, so an <c>@</c> or a brace inside it means whatever LilyPond says it
/// means and nothing else.
/// </remarks>
public sealed class LilypondSnippet
{
    internal LilypondSnippet(LilypondSnippetKind kind, string source, string fileName, string filePath,
        LilypondSnippetOptions options, bool isInline, string baseDirectory, string sourceFile,
        int lineNumber)
    {
        Kind = kind;
        Source = source ?? string.Empty;
        FileName = fileName ?? string.Empty;
        FilePath = filePath ?? string.Empty;
        Options = options ?? new LilypondSnippetOptions();
        IsInline = isInline;
        BaseDirectory = baseDirectory ?? string.Empty;
        SourceFile = sourceFile ?? string.Empty;
        LineNumber = lineNumber;
    }

    /// <summary>Which music environment this came from.</summary>
    public LilypondSnippetKind Kind { get; }

    /// <summary>
    /// The music source as the document wrote it, for <see cref="LilypondSnippetKind.Music"/>. An
    /// empty string for the two file-based kinds, which name their music instead of carrying it.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// The file name as the document wrote it, for the file-based kinds - typically a relative path
    /// such as <c>included/bar-lines.ly</c>. An empty string for <see cref="LilypondSnippetKind.Music"/>.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The full path <see cref="FileName"/> was found at on the document's search path, or an empty
    /// string when the file could not be found - which is not an error here, because a manual may
    /// name a file its build generates. A renderer handed an empty path should decline rather than
    /// guess.
    /// </summary>
    public string FilePath { get; }

    /// <summary>The snippet's bracketed options, read into named properties.</summary>
    public LilypondSnippetOptions Options { get; }

    /// <summary>
    /// True when the snippet was written inside a paragraph rather than standing on its own. This is
    /// where the snippet sits in the document, which is not the same thing as
    /// <see cref="LilypondSnippetOptions.Inline"/>, the option asking for it to be engraved small.
    /// </summary>
    public bool IsInline { get; }

    /// <summary>
    /// The directory the document was read from, which relative paths inside the music resolve
    /// against. An empty string for a document rendered from a string with no base directory.
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>The file the snippet was written in, for a renderer's own error messages.</summary>
    public string SourceFile { get; }

    /// <summary>The line the snippet started on, counting from one.</summary>
    public int LineNumber { get; }

    /// <summary>Formats the snippet for diagnostics.</summary>
    public override string ToString()
        => Kind == LilypondSnippetKind.Music
            ? $"{Kind} ({Source.Length} chars) [{Options}]"
            : $"{Kind} '{FileName}' [{Options}]";
}
