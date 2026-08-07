using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A block environment whose body is ordinary block content - <c>@quotation</c>,
/// <c>@cartouche</c>, <c>@raggedright</c>, <c>@titlepage</c> and their relatives. Environments
/// whose body is preformatted text use <see cref="PreformattedNode"/> instead.
/// </summary>
internal sealed class BlockEnvironmentNode : TexinfoNode
{
    /// <summary>Creates a block environment.</summary>
    /// <param name="commandName">The environment name without <c>@</c>.</param>
    /// <param name="kind">Which environment it is.</param>
    /// <param name="argument">The environment's line argument, such as a <c>@quotation</c> label.</param>
    /// <param name="blocks">The environment's body.</param>
    /// <param name="position">Where the environment started in the source.</param>
    public BlockEnvironmentNode(string commandName, TexinfoBlockKind kind,
        IReadOnlyList<TexinfoNode> argument, IReadOnlyList<TexinfoNode> blocks, SourcePosition position)
        : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Kind = kind;
        Argument = argument ?? new List<TexinfoNode>();
        Blocks = blocks ?? new List<TexinfoNode>();
    }

    /// <summary>The environment name without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>Which environment it is.</summary>
    public TexinfoBlockKind Kind { get; }

    /// <summary>The environment's line argument; empty for the environments that take none.</summary>
    public IReadOnlyList<TexinfoNode> Argument { get; }

    /// <summary>The environment's body.</summary>
    public IReadOnlyList<TexinfoNode> Blocks { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (TexinfoNode node in Argument)
            {
                yield return node;
            }
            foreach (TexinfoNode node in Blocks)
            {
                yield return node;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} ({Blocks.Count} blocks)";
}
