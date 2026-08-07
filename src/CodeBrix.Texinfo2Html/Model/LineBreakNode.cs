using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>A forced line break within a paragraph, from <c>@*</c>.</summary>
internal sealed class LineBreakNode : TexinfoNode
{
    /// <summary>Creates a line break node.</summary>
    /// <param name="position">Where the command appeared in the source.</param>
    public LineBreakNode(SourcePosition position) : base(position)
    {
    }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => "@* line break";
}
