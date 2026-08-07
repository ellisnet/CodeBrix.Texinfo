namespace CodeBrix.Texinfo2Html.Lexing;

/// <summary>
/// The lexical categories produced by <see cref="TexinfoLexer"/>. The token stream is lossless:
/// concatenating every token's source text reproduces the input document exactly.
/// </summary>
internal enum TexinfoTokenKind
{
    /// <summary>A run of literal text containing no <c>@</c>, brace, or line break.</summary>
    Text,

    /// <summary>
    /// An <c>@</c>-command: an alphabetic command name (<c>@code</c>, <c>@node</c>) or a single
    /// non-alphabetic character command (<c>@@</c>, <c>@{</c>, <c>@*</c>). The token covers only
    /// the command itself; any argument text follows as separate tokens.
    /// </summary>
    Command,

    /// <summary>A literal <c>{</c> opening a brace group.</summary>
    OpenBrace,

    /// <summary>A literal <c>}</c> closing a brace group.</summary>
    CloseBrace,

    /// <summary>A single line terminator.</summary>
    Newline,

    /// <summary>
    /// An <c>@c</c>/<c>@comment</c> comment running to the end of the line. A comment that is
    /// the only content on its line also swallows the line terminator, so dropping the token
    /// removes the whole line without creating a blank line.
    /// </summary>
    Comment,

    /// <summary>
    /// A block whose content was captured raw, without Texinfo tokenization: <c>@verbatim</c>,
    /// <c>@ignore</c>, raw output blocks (<c>@tex</c>, <c>@html</c>, ...), <c>@macro</c> and
    /// <c>@rmacro</c> definitions, and the lilypond-book music environments.
    /// </summary>
    RawBlock,

    /// <summary>An <c>@end name</c> block terminator for a non-raw block environment.</summary>
    EndCommand,

    /// <summary>End of the source text; always the final token.</summary>
    EndOfInput
}
