using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// One term of a two-column table entry, from <c>@item</c> or <c>@itemx</c>. A single entry can
/// carry several terms that share one description, which is what <c>@itemx</c> is for.
/// </summary>
internal sealed class TableTermNode : TexinfoNode
{
    /// <summary>Creates a table term.</summary>
    /// <param name="content">The term text.</param>
    /// <param name="isContinuation">True when the term came from <c>@itemx</c>.</param>
    /// <param name="position">Where the command appeared in the source.</param>
    public TableTermNode(IReadOnlyList<TexinfoNode> content, bool isContinuation, SourcePosition position)
        : base(position)
    {
        Content = content ?? new List<TexinfoNode>();
        IsContinuation = isContinuation;
    }

    /// <summary>
    /// The term text, without the table's format command applied - the emitter applies
    /// <see cref="TableNode.FormatCommand"/> so the same term text can be reused unchanged.
    /// </summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <summary>True when the term came from <c>@itemx</c> rather than <c>@item</c>.</summary>
    public bool IsContinuation { get; }

    /// <summary>
    /// The index entry an <c>@ftable</c> or <c>@vtable</c> files for this term, or null for a
    /// plain <c>@table</c>, which files none. It is one of the <see cref="ChildNodes"/> so that
    /// every entry the document collects is reachable from the tree, even though its content is
    /// the same node instances as <see cref="Content"/>.
    /// </summary>
    public IndexEntryNode IndexEntry { get; set; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (TexinfoNode node in Content)
            {
                yield return node;
            }
            if (IndexEntry != null)
            {
                yield return IndexEntry;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => IsContinuation ? "@itemx term" : "@item term";
}
