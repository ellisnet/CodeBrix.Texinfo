using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// One row of a <c>@multitable</c>, started by <c>@item</c> or - for a heading row -
/// <c>@headitem</c>, with cells separated by <c>@tab</c>.
/// </summary>
internal sealed class MultitableRowNode : TexinfoNode
{
    /// <summary>Creates a table row.</summary>
    /// <param name="isHeader">True when the row came from <c>@headitem</c>.</param>
    /// <param name="cells">The row's cells.</param>
    /// <param name="position">Where the row started in the source.</param>
    public MultitableRowNode(bool isHeader, IReadOnlyList<MultitableCellNode> cells, SourcePosition position)
        : base(position)
    {
        IsHeader = isHeader;
        Cells = cells ?? new List<MultitableCellNode>();
    }

    /// <summary>True when the row came from <c>@headitem</c> and holds column headings.</summary>
    public bool IsHeader { get; }

    /// <summary>The row's cells.</summary>
    public IReadOnlyList<MultitableCellNode> Cells { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Cells;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"{(IsHeader ? "header" : "row")} ({Cells.Count} cells)";
}
