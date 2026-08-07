using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>One <c>@item</c> of an <c>@itemize</c> or <c>@enumerate</c> list.</summary>
internal sealed class ListItemNode : TexinfoNode
{
    /// <summary>Creates a list item.</summary>
    /// <param name="blocks">The item's content.</param>
    /// <param name="position">Where the <c>@item</c> command appeared in the source.</param>
    public ListItemNode(IReadOnlyList<TexinfoNode> blocks, SourcePosition position) : base(position)
    {
        Blocks = blocks ?? new List<TexinfoNode>();
    }

    /// <summary>The item's content.</summary>
    public IReadOnlyList<TexinfoNode> Blocks { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Blocks;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"list item ({Blocks.Count} blocks)";
}
