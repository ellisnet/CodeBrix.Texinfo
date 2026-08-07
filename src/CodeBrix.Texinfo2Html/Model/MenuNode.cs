using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A <c>@menu</c> or <c>@detailmenu</c> block. Print output has a table of contents instead of
/// menus, so the emitter drops these; the parser still records them rather than discarding the
/// text unseen.
/// </summary>
internal sealed class MenuNode : TexinfoNode
{
    /// <summary>Creates a menu.</summary>
    /// <param name="isDetailed">True for <c>@detailmenu</c>.</param>
    /// <param name="entries">The menu's entries.</param>
    /// <param name="position">Where the menu started in the source.</param>
    public MenuNode(bool isDetailed, IReadOnlyList<MenuEntryNode> entries, SourcePosition position)
        : base(position)
    {
        IsDetailed = isDetailed;
        Entries = entries ?? new List<MenuEntryNode>();
    }

    /// <summary>True for <c>@detailmenu</c>, which lists a whole subtree rather than one level.</summary>
    public bool IsDetailed { get; }

    /// <summary>The menu's entries.</summary>
    public IReadOnlyList<MenuEntryNode> Entries { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Entries;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@menu ({Entries.Count} entries)";
}
