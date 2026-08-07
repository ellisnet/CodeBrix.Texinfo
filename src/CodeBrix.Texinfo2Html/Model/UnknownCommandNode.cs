using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A command the parser does not implement, kept in the tree so its argument text still reaches
/// the output. An unknown inline command renders as its argument; an unknown environment renders
/// as a plain block holding its content. Every one of these also produced a warning, so nothing
/// disappears silently.
/// </summary>
internal sealed class UnknownCommandNode : TexinfoNode
{
    /// <summary>Creates a node for an unimplemented command.</summary>
    /// <param name="commandName">The command name without <c>@</c>.</param>
    /// <param name="content">Whatever content the command carried.</param>
    /// <param name="isBlock">True when the command opened a block environment.</param>
    /// <param name="position">Where the command appeared in the source.</param>
    public UnknownCommandNode(string commandName, IReadOnlyList<TexinfoNode> content, bool isBlock,
        SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Content = content ?? new List<TexinfoNode>();
        IsBlock = isBlock;
    }

    /// <summary>The command name without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>Whatever content the command carried.</summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <summary>True when the command opened a block environment rather than appearing inline.</summary>
    public bool IsBlock { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Both;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Content;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"unknown @{CommandName}";
}
