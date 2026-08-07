using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A standalone instruction to the emitter, such as <c>@contents</c>, <c>@printindex</c> or
/// <c>@page</c>. The argument is kept as written because what it means differs per directive -
/// an index name for <c>@printindex</c>, a line count for <c>@sp</c>, a TeX dimension for
/// <c>@vskip</c>.
/// </summary>
internal sealed class DirectiveNode : TexinfoNode
{
    /// <summary>Creates a directive.</summary>
    /// <param name="kind">Which instruction the directive carries.</param>
    /// <param name="commandName">The command that produced it, without <c>@</c>.</param>
    /// <param name="argument">The command's argument as written, or an empty string.</param>
    /// <param name="position">Where the command appeared in the source.</param>
    public DirectiveNode(DirectiveKind kind, string commandName, string argument, SourcePosition position)
        : base(position)
    {
        Kind = kind;
        CommandName = commandName ?? string.Empty;
        Argument = argument ?? string.Empty;
    }

    /// <summary>Which instruction the directive carries.</summary>
    public DirectiveKind Kind { get; }

    /// <summary>The command that produced the directive, without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>The command's argument exactly as written; empty when it takes none.</summary>
    public string Argument { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} {Argument}".TrimEnd();
}
