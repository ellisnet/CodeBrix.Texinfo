using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A multi-column table from <c>@multitable</c>. Column widths are given either as fractions of
/// the line width, with <c>@columnfractions</c>, or as brace-delimited prototype strings whose
/// widths stand in for the columns'; only one of the two is ever populated.
/// </summary>
internal sealed class MultitableNode : TexinfoNode
{
    /// <summary>Creates a multi-column table.</summary>
    /// <param name="columnFractions">Column widths as fractions of the line width, or empty.</param>
    /// <param name="columnPrototypes">Prototype strings standing in for column widths, or empty.</param>
    /// <param name="rows">The table's rows.</param>
    /// <param name="position">Where the table started in the source.</param>
    public MultitableNode(IReadOnlyList<double> columnFractions, IReadOnlyList<string> columnPrototypes,
        IReadOnlyList<MultitableRowNode> rows, SourcePosition position) : base(position)
    {
        ColumnFractions = columnFractions ?? new List<double>();
        ColumnPrototypes = columnPrototypes ?? new List<string>();
        Rows = rows ?? new List<MultitableRowNode>();
    }

    /// <summary>Column widths as fractions of the line width; empty when prototypes were used.</summary>
    public IReadOnlyList<double> ColumnFractions { get; }

    /// <summary>Prototype strings whose widths stand in for the columns'; empty when fractions were used.</summary>
    public IReadOnlyList<string> ColumnPrototypes { get; }

    /// <summary>The table's rows.</summary>
    public IReadOnlyList<MultitableRowNode> Rows { get; }

    /// <summary>The number of columns the table was declared with.</summary>
    public int ColumnCount
        => ColumnFractions.Count > 0 ? ColumnFractions.Count : ColumnPrototypes.Count;

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Rows;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@multitable ({ColumnCount} columns, {Rows.Count} rows)";
}
