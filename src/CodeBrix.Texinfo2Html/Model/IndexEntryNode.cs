using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// An index entry from <c>@cindex</c>, <c>@findex</c> and the other index commands. Entries stay
/// in the tree where they were written, because that is the spot the printed index must link
/// back to, and they are additionally collected on the document so index building does not have
/// to walk the whole tree.
/// </summary>
internal sealed class IndexEntryNode : TexinfoNode
{
    /// <summary>Creates an index entry.</summary>
    /// <param name="indexName">The two-letter index name, such as <c>cp</c> or <c>fn</c>.</param>
    /// <param name="commandName">The command that produced the entry, without <c>@</c>.</param>
    /// <param name="content">The entry text as written.</param>
    /// <param name="sortKey">The <c>@sortas</c> key, or an empty string when none was given.</param>
    /// <param name="position">Where the command started in the source.</param>
    public IndexEntryNode(string indexName, string commandName, IReadOnlyList<TexinfoNode> content,
        string sortKey, SourcePosition position) : base(position)
    {
        IndexName = indexName ?? string.Empty;
        CommandName = commandName ?? string.Empty;
        Content = content ?? new List<TexinfoNode>();
        SortKey = sortKey ?? string.Empty;
    }

    /// <summary>The two-letter index name the entry belongs to, such as <c>cp</c> or <c>fn</c>.</summary>
    public string IndexName { get; }

    /// <summary>The command that produced the entry, without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>The entry text as written.</summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <summary>
    /// The sort key given by <c>@sortas</c> inside the entry, or an empty string when the entry
    /// sorts by its own text.
    /// </summary>
    public string SortKey { get; }

    /// <summary>
    /// The section the entry was written in, filled in by the parser as sections are built. Null
    /// for entries that appear before the first sectioning command.
    /// </summary>
    public SectionNode Section { get; set; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Both;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Content;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} -> index '{IndexName}'";
}
