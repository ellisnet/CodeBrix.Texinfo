using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A definition: one or more heading lines naming the entities being described, followed by the
/// body that describes them. <c>@deffn</c> and every one of its relatives produce this, and so
/// does a run of <c>@defline</c> lines inside a <c>@defblock</c>.
/// </summary>
/// <remarks>
/// The heading lines are held together rather than interleaved with the body because Texinfo calls
/// the <c>x</c> forms "further first lines": however far down the source a <c>@deffnx</c> is
/// written, it heads the same description as the line it continues.
/// </remarks>
internal sealed class DefinitionNode : TexinfoNode
{
    private readonly List<DefinitionHeaderNode> _headers;

    /// <summary>Creates a definition.</summary>
    /// <param name="commandName">The command that opened the definition, without <c>@</c>.</param>
    /// <param name="headers">The heading lines; at least one, and added to by the <c>x</c> forms.</param>
    /// <param name="blocks">The body describing the entities.</param>
    /// <param name="position">Where the definition started in the source.</param>
    public DefinitionNode(string commandName, List<DefinitionHeaderNode> headers,
        IReadOnlyList<TexinfoNode> blocks, SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        _headers = headers ?? new List<DefinitionHeaderNode>();
        Blocks = blocks ?? new List<TexinfoNode>();
    }

    /// <summary>The command that opened the definition, without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>The heading lines, in the order they were written.</summary>
    public IReadOnlyList<DefinitionHeaderNode> Headers => _headers;

    /// <summary>The body describing the entities named by the heading lines.</summary>
    public IReadOnlyList<TexinfoNode> Blocks { get; }

    /// <summary>Adds a further heading line, as an <c>x</c> form does.</summary>
    /// <param name="header">The heading line to add.</param>
    public void AddHeader(DefinitionHeaderNode header)
    {
        if (header != null)
        {
            _headers.Add(header);
        }
    }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (DefinitionHeaderNode header in _headers)
            {
                yield return header;
            }
            foreach (TexinfoNode node in Blocks)
            {
                yield return node;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString()
        => $"@{CommandName} ({_headers.Count} heading(s), {Blocks.Count} blocks)";
}
