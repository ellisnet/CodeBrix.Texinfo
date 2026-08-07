using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A <c>@verbatim</c> block. Unlike the other preformatted environments nothing inside is a
/// Texinfo command, so the content stays a single string exactly as it was written.
/// </summary>
internal sealed class VerbatimNode : TexinfoNode
{
    /// <summary>Creates a verbatim block.</summary>
    /// <param name="text">The block's content, exactly as written.</param>
    /// <param name="position">Where the block started in the source.</param>
    public VerbatimNode(string text, SourcePosition position) : base(position)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>The block's content, exactly as written.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@verbatim ({Text.Length} chars)";
}
