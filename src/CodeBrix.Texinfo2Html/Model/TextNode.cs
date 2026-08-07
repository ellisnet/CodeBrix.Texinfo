using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A run of literal text. Line breaks inside a paragraph survive as newline characters in these
/// nodes; collapsing them into spaces is the emitter's decision, because preformatted
/// environments need them preserved.
/// </summary>
internal sealed class TextNode : TexinfoNode
{
    /// <summary>Creates a text node.</summary>
    /// <param name="text">The literal text.</param>
    /// <param name="position">Where the text started in the source.</param>
    public TextNode(string text, SourcePosition position) : base(position)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>The literal text.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"Text '{Text}'";
}
