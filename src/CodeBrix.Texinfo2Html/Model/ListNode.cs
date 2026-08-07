using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// An <c>@itemize</c> or <c>@enumerate</c> list. Both take an optional argument that decides how
/// items are marked: a bullet command or literal character for <c>@itemize</c>, a starting
/// number or letter for <c>@enumerate</c>.
/// </summary>
internal sealed class ListNode : TexinfoNode
{
    /// <summary>Creates a list.</summary>
    /// <param name="isEnumerated">True for <c>@enumerate</c>, false for <c>@itemize</c>.</param>
    /// <param name="marker">The bullet command or character, or the starting number or letter.</param>
    /// <param name="items">The list's items.</param>
    /// <param name="position">Where the list started in the source.</param>
    public ListNode(bool isEnumerated, string marker, IReadOnlyList<ListItemNode> items,
        SourcePosition position) : base(position)
    {
        IsEnumerated = isEnumerated;
        Marker = marker ?? string.Empty;
        Items = items ?? new List<ListItemNode>();
    }

    /// <summary>True for <c>@enumerate</c>, false for <c>@itemize</c>.</summary>
    public bool IsEnumerated { get; }

    /// <summary>
    /// For <c>@itemize</c>, the bullet command name (without <c>@</c>) or the literal character
    /// that marks each item. For <c>@enumerate</c>, the starting number or letter. Empty when the
    /// list was written without an argument.
    /// </summary>
    public string Marker { get; }

    /// <summary>The list's items.</summary>
    public IReadOnlyList<ListItemNode> Items { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Items;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString()
        => $"@{(IsEnumerated ? "enumerate" : "itemize")} ({Items.Count} items)";
}
