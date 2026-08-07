using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A <c>@node</c> that was not followed by a sectioning command, and so stands on its own as a
/// named destination. When a <c>@node</c> does introduce a sectioning command the name is carried
/// on the <see cref="SectionNode"/> instead and no separate node is produced.
/// </summary>
/// <remarks>
/// The pointer arguments are recorded but unused: they describe how to walk an Info document,
/// and a PDF is read front to back.
/// </remarks>
internal sealed class NodeAnchorNode : TexinfoNode
{
    /// <summary>Creates a standalone node marker.</summary>
    /// <param name="nodeName">The node's name.</param>
    /// <param name="next">The next-node pointer as written, or an empty string.</param>
    /// <param name="previous">The previous-node pointer as written, or an empty string.</param>
    /// <param name="up">The parent-node pointer as written, or an empty string.</param>
    /// <param name="position">Where the command appeared in the source.</param>
    public NodeAnchorNode(string nodeName, string next, string previous, string up, SourcePosition position)
        : base(position)
    {
        NodeName = nodeName ?? string.Empty;
        Next = next ?? string.Empty;
        Previous = previous ?? string.Empty;
        Up = up ?? string.Empty;
    }

    /// <summary>The node's name, as cross references will spell it.</summary>
    public string NodeName { get; }

    /// <summary>The next-node pointer as written; recorded but unused.</summary>
    public string Next { get; }

    /// <summary>The previous-node pointer as written; recorded but unused.</summary>
    public string Previous { get; }

    /// <summary>The parent-node pointer as written; recorded but unused.</summary>
    public string Up { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@node {NodeName}";
}
