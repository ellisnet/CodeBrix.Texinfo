using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A paragraph of running text. Line breaks from the source survive inside the content as
/// newline characters; the emitter collapses them, because preformatted environments need the
/// same text nodes with the breaks intact.
/// </summary>
internal sealed class ParagraphNode : TexinfoNode
{
    /// <summary>Creates a paragraph.</summary>
    /// <param name="content">The paragraph's inline content.</param>
    /// <param name="alignment">How the paragraph's lines are aligned.</param>
    /// <param name="suppressIndent">True when <c>@noindent</c> preceded the paragraph.</param>
    /// <param name="position">Where the paragraph's first content started in the source.</param>
    public ParagraphNode(IReadOnlyList<TexinfoNode> content, ParagraphAlignment alignment,
        bool suppressIndent, SourcePosition position) : base(position)
    {
        Content = content ?? new List<TexinfoNode>();
        Alignment = alignment;
        SuppressIndent = suppressIndent;
    }

    /// <summary>The paragraph's inline content.</summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <summary>How the paragraph's lines are aligned.</summary>
    public ParagraphAlignment Alignment { get; }

    /// <summary>True when <c>@noindent</c> asked for the first line not to be indented.</summary>
    public bool SuppressIndent { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Content;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"paragraph ({Content.Count} nodes)";
}
