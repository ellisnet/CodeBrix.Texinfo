using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A lilypond-book music environment: <c>@lilypond</c> in either its block or its inline brace
/// form, <c>@lilypondfile</c>, or <c>@musicxmlfile</c>. The content was captured raw by the lexer
/// and is passed through untouched - it is music source, not Texinfo. The bracketed option list
/// is likewise kept verbatim; parsing it and handing the snippet to a renderer belongs to the
/// snippet layer rather than to the document parser.
/// </summary>
internal sealed class MusicSnippetNode : TexinfoNode
{
    /// <summary>Creates a music snippet node.</summary>
    /// <param name="commandName">The environment name without <c>@</c>.</param>
    /// <param name="rawOptions">The bracketed option list as written, including its brackets.</param>
    /// <param name="content">The music source, or the file name for the file-based commands.</param>
    /// <param name="isInlineForm">True when the brace form was used rather than a block.</param>
    /// <param name="position">Where the environment started in the source.</param>
    public MusicSnippetNode(string commandName, string rawOptions, string content, bool isInlineForm,
        SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        RawOptions = rawOptions ?? string.Empty;
        Content = content ?? string.Empty;
        IsInlineForm = isInlineForm;
    }

    /// <summary>The environment name without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>The bracketed option list exactly as written, brackets included; empty when absent.</summary>
    public string RawOptions { get; }

    /// <summary>The music source, or the referenced file name for <c>@lilypondfile</c> and <c>@musicxmlfile</c>.</summary>
    public string Content { get; }

    /// <summary>True when the snippet used the inline brace form rather than a block.</summary>
    public bool IsInlineForm { get; }

    /// <summary>True when the snippet names a file instead of carrying music source.</summary>
    public bool IsFileReference => CommandName == "lilypondfile" || CommandName == "musicxmlfile";

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Both;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName}{RawOptions} ({Content.Length} chars)";
}
