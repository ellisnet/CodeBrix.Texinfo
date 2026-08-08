using System;
using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Lexing;

/// <summary>
/// Converts Texinfo source text into a lossless stream of <see cref="TexinfoToken"/> instances.
/// The lexer captures raw-content blocks (<c>@verbatim</c>, <c>@ignore</c>, raw output blocks,
/// macro definition bodies, and lilypond-book music environments) as single tokens so that no
/// later stage ever tokenizes text that is not Texinfo.
/// </summary>
internal sealed class TexinfoLexer
{
    private static readonly HashSet<string> RawOutputBlocks =
        new HashSet<string>(StringComparer.Ordinal) { "tex", "html", "xml", "docbook", "latex" };

    private static readonly HashSet<string> MusicBlocks =
        new HashSet<string>(StringComparer.Ordinal) { "lilypond", "lilypondfile", "musicxmlfile" };

    private readonly string _sourceName;
    private readonly string _text;
    private readonly TexinfoWarningCollection _warnings;
    private int _index;
    private int _line = 1;
    private int _column = 1;
    private bool _lineHasContent;

    /// <summary>Creates a lexer over the given source, reporting problems to the given collection.</summary>
    /// <param name="source">The source text to tokenize.</param>
    /// <param name="warnings">Receives lexical warnings; lexing itself never throws.</param>
    public TexinfoLexer(TexinfoSourceText source, TexinfoWarningCollection warnings)
    {
        _sourceName = source.Name;
        _text = source.Text;
        _warnings = warnings;
    }

    /// <summary>True when the named command opens a raw-content block when it starts a line.</summary>
    /// <param name="name">The command name without <c>@</c>.</param>
    public static bool IsRawBlockCommand(string name)
        => RawOutputBlocks.Contains(name)
           || MusicBlocks.Contains(name)
           || name == "verbatim"
           || name == "ignore"
           || IsMacroDefinitionCommand(name);

    /// <summary>True for the three commands that open a macro definition body.</summary>
    /// <param name="name">The command name without <c>@</c>.</param>
    public static bool IsMacroDefinitionCommand(string name)
        => name == "macro" || name == "rmacro" || name == "linemacro";

    /// <summary>Tokenizes the whole source and returns the tokens, ending with EndOfInput.</summary>
    public List<TexinfoToken> Lex()
    {
        List<TexinfoToken> tokens = new List<TexinfoToken>();
        while (_index < _text.Length)
        {
            char c = _text[_index];
            if (c == '\n')
            {
                tokens.Add(new TexinfoToken(TexinfoTokenKind.Newline, "\n", Here(), !_lineHasContent));
                Advance();
                _lineHasContent = false;
            }
            else if (c == '@')
            {
                LexAtCommand(tokens);
            }
            else if (c == '{')
            {
                tokens.Add(new TexinfoToken(TexinfoTokenKind.OpenBrace, "{", Here(), !_lineHasContent));
                Advance();
                _lineHasContent = true;
            }
            else if (c == '}')
            {
                tokens.Add(new TexinfoToken(TexinfoTokenKind.CloseBrace, "}", Here(), !_lineHasContent));
                Advance();
                _lineHasContent = true;
            }
            else
            {
                LexTextRun(tokens);
            }
        }
        tokens.Add(new TexinfoToken(TexinfoTokenKind.EndOfInput, string.Empty, Here(), !_lineHasContent));
        return tokens;
    }

    private SourcePosition Here() => new SourcePosition(_sourceName, _line, _column);

    private void Advance()
    {
        if (_text[_index] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        _index++;
    }

    private void AdvanceTo(int target)
    {
        while (_index < target && _index < _text.Length)
        {
            Advance();
        }
    }

    private void LexTextRun(List<TexinfoToken> tokens)
    {
        SourcePosition start = Here();
        bool atLineStart = !_lineHasContent;
        int begin = _index;
        bool hasNonWhitespace = false;
        while (_index < _text.Length)
        {
            char c = _text[_index];
            if (c == '\n' || c == '@' || c == '{' || c == '}')
            {
                break;
            }
            if (!char.IsWhiteSpace(c))
            {
                hasNonWhitespace = true;
            }
            Advance();
        }
        tokens.Add(new TexinfoToken(TexinfoTokenKind.Text, _text.Substring(begin, _index - begin), start, atLineStart));
        if (hasNonWhitespace)
        {
            _lineHasContent = true;
        }
    }

    private void LexAtCommand(List<TexinfoToken> tokens)
    {
        SourcePosition start = Here();
        bool atLineStart = !_lineHasContent;
        if (_index + 1 >= _text.Length)
        {
            _warnings.Add(TexinfoWarningCategory.Syntax, start, "'@' at end of input.");
            tokens.Add(new TexinfoToken(TexinfoTokenKind.Text, "@", start, atLineStart));
            Advance();
            _lineHasContent = true;
            return;
        }

        char next = _text[_index + 1];
        if (!IsCommandStartChar(next))
        {
            // A single-character command such as @@, @{, @}, @*, @-, or @ followed by
            // whitespace. An @ that ends a line joins the two lines like a space.
            Advance();
            Advance();
            tokens.Add(new TexinfoToken(TexinfoTokenKind.Command, next.ToString(), start, atLineStart));
            _lineHasContent = next != '\n';
            return;
        }

        Advance();
        string name = ScanCommandName();

        if (name == "end")
        {
            LexEndCommand(tokens, start, atLineStart);
            return;
        }

        if (name == "c" || name == "comment")
        {
            LexComment(tokens, start, atLineStart, name);
            return;
        }

        if (name == "verb")
        {
            LexVerb(tokens, start, atLineStart);
            return;
        }

        if (MusicBlocks.Contains(name))
        {
            LexMusicBlock(tokens, start, atLineStart, name);
            return;
        }

        if (atLineStart && IsRawBlockCommand(name))
        {
            LexLineRawBlock(tokens, start, name);
            return;
        }

        tokens.Add(new TexinfoToken(TexinfoTokenKind.Command, name, start, atLineStart));
        _lineHasContent = true;
    }

    private static bool IsCommandStartChar(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private static bool IsCommandNameChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

    private string ScanCommandName()
    {
        int begin = _index;
        while (_index < _text.Length && IsCommandNameChar(_text[_index]))
        {
            Advance();
        }
        return _text.Substring(begin, _index - begin);
    }

    private void LexEndCommand(List<TexinfoToken> tokens, SourcePosition start, bool atLineStart)
    {
        int save = _index;
        int saveLine = _line;
        int saveColumn = _column;
        while (_index < _text.Length && (_text[_index] == ' ' || _text[_index] == '\t'))
        {
            Advance();
        }
        if (_index < _text.Length && IsCommandStartChar(_text[_index]))
        {
            string blockName = ScanCommandName();
            tokens.Add(new TexinfoToken(TexinfoTokenKind.EndCommand, blockName, start, atLineStart));
            _lineHasContent = true;
            return;
        }
        _index = save;
        _line = saveLine;
        _column = saveColumn;
        _warnings.Add(TexinfoWarningCategory.Syntax, start, "'@end' is not followed by a block name.");
        tokens.Add(new TexinfoToken(TexinfoTokenKind.Command, "end", start, atLineStart));
        _lineHasContent = true;
    }

    private void LexComment(List<TexinfoToken> tokens, SourcePosition start, bool atLineStart, string commandName)
    {
        int begin = _index;
        while (_index < _text.Length && _text[_index] != '\n')
        {
            Advance();
        }
        string commentText = _text.Substring(begin, _index - begin);
        bool wholeLine = atLineStart;
        if (wholeLine && _index < _text.Length)
        {
            Advance();
            _lineHasContent = false;
        }
        tokens.Add(new TexinfoToken(TexinfoTokenKind.Comment, commentText, start, atLineStart)
        {
            IsWholeLineComment = wholeLine,
            CommentCommand = commandName
        });
        if (!wholeLine)
        {
            _lineHasContent = true;
        }
    }

    private void LexLineRawBlock(List<TexinfoToken> tokens, SourcePosition start, string name)
    {
        // The command starts a line, so everything after the name up to the end of the line is
        // the opening argument, and the body runs until the matching '@end name' line.
        int argBegin = _index;
        while (_index < _text.Length && _text[_index] != '\n')
        {
            Advance();
        }
        string rawArgument = _text.Substring(argBegin, _index - argBegin);
        if (_index < _text.Length)
        {
            Advance();
        }
        bool macroFamily = IsMacroDefinitionCommand(name);
        string content = CaptureBlockBody(name, macroFamily, name == "ignore", start);
        tokens.Add(new TexinfoToken(TexinfoTokenKind.RawBlock, name, start, atLineStart: true)
        {
            RawArgument = rawArgument,
            RawContent = content
        });
        _lineHasContent = false;
    }

    private string CaptureBlockBody(string name, bool macroFamily, bool sameNameNesting, SourcePosition start)
    {
        int contentBegin = _index;
        int depth = 0;
        while (_index < _text.Length)
        {
            int lineBegin = _index;
            int lineEnd = _text.IndexOf('\n', lineBegin);
            if (lineEnd < 0)
            {
                lineEnd = _text.Length;
            }
            string line = _text.Substring(lineBegin, lineEnd - lineBegin).Trim();

            bool isTerminator = macroFamily
                ? IsEndLine(line, "macro") || IsEndLine(line, "rmacro") || IsEndLine(line, "linemacro")
                : IsEndLine(line, name);
            if (isTerminator)
            {
                if (depth == 0)
                {
                    string content = _text.Substring(contentBegin, lineBegin - contentBegin);
                    AdvanceTo(lineEnd < _text.Length ? lineEnd + 1 : lineEnd);
                    return content;
                }
                depth--;
            }
            else if (macroFamily && (IsOpenerLine(line, "macro") || IsOpenerLine(line, "rmacro")
                                     || IsOpenerLine(line, "linemacro")))
            {
                depth++;
            }
            else if (sameNameNesting && IsOpenerLine(line, name))
            {
                depth++;
            }

            AdvanceTo(lineEnd < _text.Length ? lineEnd + 1 : lineEnd);
        }
        _warnings.Add(TexinfoWarningCategory.Syntax, start, $"'@{name}' block is missing its '@end {name}'.");
        return _text.Substring(contentBegin);
    }

    private static bool IsEndLine(string trimmedLine, string name)
    {
        if (!trimmedLine.StartsWith("@end", StringComparison.Ordinal))
        {
            return false;
        }
        string rest = trimmedLine.Substring(4);
        if (rest.Length == 0 || (rest[0] != ' ' && rest[0] != '\t'))
        {
            return false;
        }
        return rest.Trim() == name;
    }

    private static bool IsOpenerLine(string trimmedLine, string name)
    {
        string command = "@" + name;
        if (!trimmedLine.StartsWith(command, StringComparison.Ordinal))
        {
            return false;
        }
        if (trimmedLine.Length == command.Length)
        {
            return true;
        }
        char after = trimmedLine[command.Length];
        return after == ' ' || after == '\t' || after == '{';
    }

    /// <summary>
    /// Moves past whitespace when the given character follows it, leaving the position untouched
    /// when it does not. The lilypond-book environments are written with the option list and the
    /// brace group separated by spaces or by a line break, so the lexer has to look past both.
    /// </summary>
    private void SkipWhitespaceBefore(char expected, bool acrossLines)
    {
        int savedIndex = _index;
        int savedLine = _line;
        int savedColumn = _column;
        while (_index < _text.Length
               && char.IsWhiteSpace(_text[_index])
               && (acrossLines || _text[_index] != '\n'))
        {
            Advance();
        }
        if (_index < _text.Length && _text[_index] == expected)
        {
            return;
        }
        _index = savedIndex;
        _line = savedLine;
        _column = savedColumn;
    }

    /// <summary>
    /// Captures <c>@verb{&lt;delimiter&gt;text&lt;delimiter&gt;}</c>. The character straight after the
    /// brace is the delimiter the author chose, and the text runs to the next occurrence of that
    /// delimiter followed by the closing brace. Nothing between them is Texinfo - that is the whole
    /// point of the command - so it is captured here, where no later stage can tokenize it.
    /// </summary>
    private void LexVerb(List<TexinfoToken> tokens, SourcePosition start, bool atLineStart)
    {
        if (_index >= _text.Length || _text[_index] != '{')
        {
            _warnings.Add(TexinfoWarningCategory.Syntax, start,
                "'@verb' is not followed by a brace group; treating it as a plain command.");
            tokens.Add(new TexinfoToken(TexinfoTokenKind.Command, "verb", start, atLineStart));
            _lineHasContent = true;
            return;
        }
        Advance();
        if (_index >= _text.Length || _text[_index] == '}')
        {
            _warnings.Add(TexinfoWarningCategory.Syntax, start,
                "'@verb' has no delimiter character after its opening brace; it produced nothing.");
            if (_index < _text.Length)
            {
                Advance();
            }
            tokens.Add(new TexinfoToken(TexinfoTokenKind.RawBlock, "verb", start, atLineStart)
            {
                IsBraceRawBlock = true
            });
            _lineHasContent = true;
            return;
        }

        char delimiter = _text[_index];
        Advance();
        int contentBegin = _index;
        string terminator = delimiter.ToString() + "}";
        int end = _text.IndexOf(terminator, _index, StringComparison.Ordinal);
        string content;
        if (end < 0)
        {
            _warnings.Add(TexinfoWarningCategory.Syntax, start,
                $"'@verb' is missing its closing '{terminator}'; the rest of the file was taken as its text.");
            content = _text.Substring(contentBegin);
            AdvanceTo(_text.Length);
        }
        else
        {
            content = _text.Substring(contentBegin, end - contentBegin);
            AdvanceTo(end + terminator.Length);
        }
        tokens.Add(new TexinfoToken(TexinfoTokenKind.RawBlock, "verb", start, atLineStart)
        {
            RawArgument = delimiter.ToString(),
            RawContent = content,
            IsBraceRawBlock = true
        });
        _lineHasContent = true;
    }

    private void LexMusicBlock(List<TexinfoToken> tokens, SourcePosition start, bool atLineStart, string name)
    {
        // lilypond-book environments: an optional single-line [options] group, then either an
        // inline {music} group (captured raw to the matching brace) or - for @lilypond at the
        // start of a line - a body running to '@end lilypond'.
        string rawArgument = string.Empty;
        SkipWhitespaceBefore('[', acrossLines: false);
        if (_index < _text.Length && _text[_index] == '[')
        {
            int optionsBegin = _index;
            int lineEnd = _text.IndexOf('\n', _index);
            if (lineEnd < 0)
            {
                lineEnd = _text.Length;
            }
            int closeBracket = _text.IndexOf(']', _index);
            if (closeBracket >= 0 && closeBracket < lineEnd)
            {
                AdvanceTo(closeBracket + 1);
                rawArgument = _text.Substring(optionsBegin, _index - optionsBegin);
            }
            else
            {
                _warnings.Add(TexinfoWarningCategory.Syntax, start,
                    $"'@{name}' options are missing their closing ']' on the same line.");
            }
        }

        // @lilypondfile and @musicxmlfile always name a file in a brace group, and the corpus
        // routinely puts that group on the line after the options, so whitespace is skipped
        // across lines for them. @lilypond cannot do the same: at the start of a line it opens a
        // block whose music body may itself begin with '{'.
        SkipWhitespaceBefore('{', acrossLines: name != "lilypond");

        if (_index < _text.Length && _text[_index] == '{')
        {
            Advance();
            int contentBegin = _index;
            int depth = 1;
            while (_index < _text.Length && depth > 0)
            {
                char c = _text[_index];
                if (c == '@' && _index + 1 < _text.Length)
                {
                    Advance();
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
                Advance();
            }
            string content;
            if (depth == 0)
            {
                content = _text.Substring(contentBegin, _index - contentBegin);
                Advance();
            }
            else
            {
                content = _text.Substring(contentBegin);
                _warnings.Add(TexinfoWarningCategory.Syntax, start,
                    $"'@{name}' brace group is missing its closing '}}'.");
            }
            tokens.Add(new TexinfoToken(TexinfoTokenKind.RawBlock, name, start, atLineStart)
            {
                RawArgument = rawArgument,
                RawContent = content,
                IsBraceRawBlock = true
            });
            _lineHasContent = true;
            return;
        }

        if (name == "lilypond" && atLineStart)
        {
            int argBegin = _index;
            while (_index < _text.Length && _text[_index] != '\n')
            {
                Advance();
            }
            string trailing = _text.Substring(argBegin, _index - argBegin);
            if (trailing.Trim().Length > 0)
            {
                _warnings.Add(TexinfoWarningCategory.Syntax, start,
                    "Unexpected text after '@lilypond' options; treating the block as line form.");
            }
            if (_index < _text.Length)
            {
                Advance();
            }
            string content = CaptureBlockBody(name, macroFamily: false, sameNameNesting: false, start);
            tokens.Add(new TexinfoToken(TexinfoTokenKind.RawBlock, name, start, atLineStart)
            {
                RawArgument = rawArgument + trailing,
                RawContent = content
            });
            _lineHasContent = false;
            return;
        }

        _warnings.Add(TexinfoWarningCategory.Syntax, start,
            $"'@{name}' is not followed by a brace group; treating it as a plain command.");
        tokens.Add(new TexinfoToken(TexinfoTokenKind.Command, name, start, atLineStart));
        _lineHasContent = true;
    }
}
