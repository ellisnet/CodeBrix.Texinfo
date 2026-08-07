using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A two-column table from <c>@table</c>, <c>@ftable</c> or <c>@vtable</c>. The command takes a
/// formatting command as its argument - <c>@table @code</c> is the common case - which is applied
/// to every term. The indexed variants additionally file each term in an index.
/// </summary>
internal sealed class TableNode : TexinfoNode
{
    /// <summary>Creates a table.</summary>
    /// <param name="commandName">The command name without <c>@</c>.</param>
    /// <param name="formatCommand">The command applied to each term, without <c>@</c>.</param>
    /// <param name="indexName">The index terms are filed in, or an empty string for plain tables.</param>
    /// <param name="entries">The table's entries.</param>
    /// <param name="position">Where the table started in the source.</param>
    public TableNode(string commandName, string formatCommand, string indexName,
        IReadOnlyList<TableEntryNode> entries, SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        FormatCommand = formatCommand ?? string.Empty;
        IndexName = indexName ?? string.Empty;
        Entries = entries ?? new List<TableEntryNode>();
    }

    /// <summary>The command name without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>
    /// The command applied to each term, without <c>@</c> - <c>code</c>, <c>samp</c>, <c>asis</c>
    /// and so on. Empty when the table was written without an argument.
    /// </summary>
    public string FormatCommand { get; }

    /// <summary>
    /// The two-letter index that <c>@ftable</c> and <c>@vtable</c> file their terms in; empty for
    /// a plain <c>@table</c>.
    /// </summary>
    public string IndexName { get; }

    /// <summary>The table's entries.</summary>
    public IReadOnlyList<TableEntryNode> Entries { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Entries;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} @{FormatCommand} ({Entries.Count} entries)";
}
