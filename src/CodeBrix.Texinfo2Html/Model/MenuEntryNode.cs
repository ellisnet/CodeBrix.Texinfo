using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// One entry of a <c>@menu</c>. Menus exist to navigate Info output and are dropped from print
/// output, but they are parsed rather than skipped so the structure they describe stays
/// available to later passes.
/// </summary>
internal sealed class MenuEntryNode : TexinfoNode
{
    /// <summary>Creates a menu entry.</summary>
    /// <param name="nodeName">The node the entry points at.</param>
    /// <param name="label">The entry's label when it differs from the node name.</param>
    /// <param name="description">The description that follows the entry, as plain text.</param>
    /// <param name="position">Where the entry started in the source.</param>
    public MenuEntryNode(string nodeName, string label, string description, SourcePosition position)
        : base(position)
    {
        NodeName = nodeName ?? string.Empty;
        Label = label ?? string.Empty;
        Description = description ?? string.Empty;
    }

    /// <summary>The node the entry points at.</summary>
    public string NodeName { get; }

    /// <summary>The entry's label; empty when the node name is also the label.</summary>
    public string Label { get; }

    /// <summary>The description that follows the entry, as plain text.</summary>
    public string Description { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"menu entry -> '{NodeName}'";
}
