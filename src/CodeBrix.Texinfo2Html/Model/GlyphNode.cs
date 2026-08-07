using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A command that stands for a fixed piece of text and takes no argument, such as
/// <c>@dots{}</c>, <c>@copyright{}</c> or <c>@minus{}</c>. The resolved text is carried on the
/// node so emitters need no lookup table of their own, while the command name remains available
/// for diagnostics.
/// </summary>
internal sealed class GlyphNode : TexinfoNode
{
    /// <summary>Creates a glyph node.</summary>
    /// <param name="commandName">The command name without <c>@</c>.</param>
    /// <param name="text">The text the command stands for.</param>
    /// <param name="position">Where the command started in the source.</param>
    public GlyphNode(string commandName, string text, SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Text = text ?? string.Empty;
    }

    /// <summary>The command name without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>The text the command stands for.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName}{{}} '{Text}'";
}
