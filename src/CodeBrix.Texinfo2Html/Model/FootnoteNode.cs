using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A footnote from <c>@footnote{...}</c>. The node marks the place in the text where the
/// reference mark goes; the emitter decides where the note body is printed. Numbers are assigned
/// in document order while parsing, so they are stable regardless of how the emitter groups them.
/// </summary>
internal sealed class FootnoteNode : TexinfoNode
{
    /// <summary>Creates a footnote node.</summary>
    /// <param name="number">The footnote's one-based number in document order.</param>
    /// <param name="content">The note's content.</param>
    /// <param name="position">Where the command started in the source.</param>
    public FootnoteNode(int number, IReadOnlyList<TexinfoNode> content, SourcePosition position)
        : base(position)
    {
        Number = number;
        Content = content ?? new List<TexinfoNode>();
    }

    /// <summary>The footnote's one-based number in document order.</summary>
    public int Number { get; }

    /// <summary>The note's content.</summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Content;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"footnote {Number}";
}
