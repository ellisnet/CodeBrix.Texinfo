using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>One cell of a <c>@multitable</c> row.</summary>
internal sealed class MultitableCellNode : TexinfoNode
{
    /// <summary>Creates a table cell.</summary>
    /// <param name="blocks">The cell's content.</param>
    /// <param name="position">Where the cell's content started in the source.</param>
    public MultitableCellNode(IReadOnlyList<TexinfoNode> blocks, SourcePosition position) : base(position)
    {
        Blocks = blocks ?? new List<TexinfoNode>();
    }

    /// <summary>The cell's content.</summary>
    public IReadOnlyList<TexinfoNode> Blocks { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Blocks;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"cell ({Blocks.Count} blocks)";
}
