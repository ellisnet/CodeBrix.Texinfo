using System;
using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html.Snippets;

/// <summary>
/// One music environment worked out far enough for the emitter to place it: what the document asked
/// for, what was engraved, and what text to show. Everything that needed a decision was decided by
/// <see cref="SnippetRenderCoordinator"/>; the emitter only writes it out.
/// </summary>
internal sealed class PreparedSnippet
{
    /// <summary>Creates a prepared snippet.</summary>
    /// <param name="options">The snippet's options, read into named properties.</param>
    /// <param name="imagePaths">Engraved pictures, as paths relative to the document.</param>
    /// <param name="sourceText">The music source, or the file the snippet names.</param>
    /// <param name="showSource">Whether the source text is to be written as well as the pictures.</param>
    public PreparedSnippet(LilypondSnippetOptions options, IReadOnlyList<string> imagePaths,
        string sourceText, bool showSource)
    {
        Options = options ?? new LilypondSnippetOptions();
        ImagePaths = imagePaths ?? Array.Empty<string>();
        SourceText = sourceText ?? string.Empty;
        ShowSource = showSource;
    }

    /// <summary>The snippet's options as the document wrote them.</summary>
    public LilypondSnippetOptions Options { get; }

    /// <summary>
    /// The pictures the snippet engraved to, relative to the directory the document is written into,
    /// in the order they should appear. Empty when nothing engraved it.
    /// </summary>
    public IReadOnlyList<string> ImagePaths { get; }

    /// <summary>
    /// The music source to show - the snippet's own text, the contents of the file it names when it
    /// asked for that, or the command naming a file that could not be read.
    /// </summary>
    public string SourceText { get; }

    /// <summary>
    /// True when the source text is to be written: always when nothing was engraved, and alongside
    /// the pictures when the document asked for <c>verbatim</c>.
    /// </summary>
    public bool ShowSource { get; }
}
