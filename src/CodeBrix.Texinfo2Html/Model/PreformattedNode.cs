using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A preformatted environment - <c>@example</c>, <c>@lisp</c>, <c>@display</c>, <c>@format</c>
/// and their small variants. Texinfo still processes commands inside these environments, so the
/// content is a flat run of inline nodes rather than raw text, with every line break preserved.
/// A <c>@group</c> written inside one of these environments is transparent: it exists to keep the
/// block on one page and contributes no structure, so its content is spliced in directly.
/// </summary>
internal sealed class PreformattedNode : TexinfoNode
{
    /// <summary>Creates a preformatted environment.</summary>
    /// <param name="commandName">The environment name without <c>@</c>.</param>
    /// <param name="kind">Which environment it is.</param>
    /// <param name="content">The environment's content, with line breaks preserved.</param>
    /// <param name="position">Where the environment started in the source.</param>
    public PreformattedNode(string commandName, TexinfoBlockKind kind,
        IReadOnlyList<TexinfoNode> content, SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Kind = kind;
        Content = content ?? new List<TexinfoNode>();
    }

    /// <summary>The environment name without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>Which environment it is.</summary>
    public TexinfoBlockKind Kind { get; }

    /// <summary>The environment's content, with every line break preserved.</summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Content;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} preformatted ({Content.Count} nodes)";
}
