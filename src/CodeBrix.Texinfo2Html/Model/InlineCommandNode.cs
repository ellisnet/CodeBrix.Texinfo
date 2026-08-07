using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// An inline command that wraps content, such as <c>@code{...}</c> or <c>@emph{...}</c>. The
/// original command name is kept alongside the semantic <see cref="Style"/> so diagnostics and
/// round-trip debugging can name the command the document actually used.
/// </summary>
internal sealed class InlineCommandNode : TexinfoNode
{
    /// <summary>Creates an inline command node.</summary>
    /// <param name="commandName">The command name without <c>@</c>.</param>
    /// <param name="style">The semantic role the command plays.</param>
    /// <param name="content">The command's parsed argument.</param>
    /// <param name="position">Where the command started in the source.</param>
    public InlineCommandNode(string commandName, InlineStyle style, IReadOnlyList<TexinfoNode> content,
        SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Style = style;
        Content = content ?? new List<TexinfoNode>();
    }

    /// <summary>The command name without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>The semantic role the command plays.</summary>
    public InlineStyle Style { get; }

    /// <summary>The command's parsed argument.</summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Content;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} ({Style})";
}
