using System.Text;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Lexing;

/// <summary>
/// One lexical token of Texinfo source. Tokens are lossless: <see cref="ToSourceText"/> returns
/// the exact source characters the token covers, so a token stream can be turned back into
/// equivalent Texinfo text (the basis of the expansion dump used for testing and debugging).
/// </summary>
internal sealed class TexinfoToken
{
    /// <summary>Creates a token.</summary>
    /// <param name="kind">The lexical category.</param>
    /// <param name="value">The token's primary text (see <see cref="Value"/>).</param>
    /// <param name="position">Where the token starts in its source.</param>
    /// <param name="atLineStart">Whether only whitespace precedes the token on its line.</param>
    public TexinfoToken(TexinfoTokenKind kind, string value, SourcePosition position, bool atLineStart)
    {
        Kind = kind;
        Value = value ?? string.Empty;
        Position = position;
        AtLineStart = atLineStart;
    }

    /// <summary>The lexical category of the token.</summary>
    public TexinfoTokenKind Kind { get; }

    /// <summary>
    /// The token's primary text. For <see cref="TexinfoTokenKind.Text"/> the literal text; for
    /// <see cref="TexinfoTokenKind.Command"/>, <see cref="TexinfoTokenKind.RawBlock"/> and
    /// <see cref="TexinfoTokenKind.EndCommand"/> the command name (without <c>@</c>); for
    /// <see cref="TexinfoTokenKind.Comment"/> the comment text after the command name.
    /// </summary>
    public string Value { get; }

    /// <summary>Where the token starts in its source.</summary>
    public SourcePosition Position { get; }

    /// <summary>Whether only whitespace precedes the token on its line.</summary>
    public bool AtLineStart { get; }

    /// <summary>
    /// For <see cref="TexinfoTokenKind.RawBlock"/>: the unparsed text that followed the command
    /// name on its opening line - the <c>[options]</c> of a lilypond block, or the name and
    /// parameter list of a macro definition, or the single delimiter character of a <c>@verb</c>.
    /// Empty for other kinds.
    /// </summary>
    public string RawArgument { get; init; } = string.Empty;

    /// <summary>
    /// For <see cref="TexinfoTokenKind.RawBlock"/>: the captured raw content, excluding the
    /// opening line and the terminator. Empty for other kinds.
    /// </summary>
    public string RawContent { get; init; } = string.Empty;

    /// <summary>
    /// For <see cref="TexinfoTokenKind.RawBlock"/>: true when the block used the inline brace
    /// form (<c>@lilypond[...]{...}</c>) rather than a line-oriented <c>@end</c>-terminated body.
    /// </summary>
    public bool IsBraceRawBlock { get; init; }

    /// <summary>
    /// For <see cref="TexinfoTokenKind.Comment"/>: true when the comment was the only content on
    /// its line, in which case the token also covers the line terminator.
    /// </summary>
    public bool IsWholeLineComment { get; init; }

    /// <summary>
    /// For <see cref="TexinfoTokenKind.Comment"/>: the command spelling that introduced the
    /// comment (<c>c</c> or <c>comment</c>). Empty for other kinds.
    /// </summary>
    public string CommentCommand { get; init; } = string.Empty;

    /// <summary>Returns the exact source text this token covers.</summary>
    public string ToSourceText()
    {
        switch (Kind)
        {
            case TexinfoTokenKind.Text:
                return Value;
            case TexinfoTokenKind.Command:
                return "@" + Value;
            case TexinfoTokenKind.OpenBrace:
                return "{";
            case TexinfoTokenKind.CloseBrace:
                return "}";
            case TexinfoTokenKind.Newline:
                return "\n";
            case TexinfoTokenKind.Comment:
                return "@" + CommentCommand + Value + (IsWholeLineComment ? "\n" : string.Empty);
            case TexinfoTokenKind.RawBlock:
                if (Value == "verb")
                {
                    //The delimiter is what @verb wrote around its text, and putting it back is
                    //what makes the reconstruction read the same way round.
                    return "@verb{" + RawArgument + RawContent + RawArgument + "}";
                }
                if (IsBraceRawBlock)
                {
                    return "@" + Value + RawArgument + "{" + RawContent + "}";
                }
                StringBuilder builder = new StringBuilder();
                builder.Append('@').Append(Value).Append(RawArgument).Append('\n');
                builder.Append(RawContent);
                builder.Append("@end ").Append(Value).Append('\n');
                return builder.ToString();
            case TexinfoTokenKind.EndCommand:
                return "@end " + Value;
            default:
                return string.Empty;
        }
    }

    /// <summary>Formats the token for diagnostics as <c>kind 'text' at position</c>.</summary>
    public override string ToString() => $"{Kind} '{ToSourceText()}' at {Position}";
}
