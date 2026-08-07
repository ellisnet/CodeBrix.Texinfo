using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>One entry of a two-column table: its terms and the description they share.</summary>
internal sealed class TableEntryNode : TexinfoNode
{
    /// <summary>Creates a table entry.</summary>
    /// <param name="terms">The entry's terms, the first from <c>@item</c> and the rest from <c>@itemx</c>.</param>
    /// <param name="blocks">The description shared by those terms.</param>
    /// <param name="position">Where the entry's first term appeared in the source.</param>
    public TableEntryNode(IReadOnlyList<TableTermNode> terms, IReadOnlyList<TexinfoNode> blocks,
        SourcePosition position) : base(position)
    {
        Terms = terms ?? new List<TableTermNode>();
        Blocks = blocks ?? new List<TexinfoNode>();
    }

    /// <summary>The entry's terms.</summary>
    public IReadOnlyList<TableTermNode> Terms { get; }

    /// <summary>The description shared by the entry's terms.</summary>
    public IReadOnlyList<TexinfoNode> Blocks { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (TableTermNode term in Terms)
            {
                yield return term;
            }
            foreach (TexinfoNode node in Blocks)
            {
                yield return node;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"table entry ({Terms.Count} terms)";
}
