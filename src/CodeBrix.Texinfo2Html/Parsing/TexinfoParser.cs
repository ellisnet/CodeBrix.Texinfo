using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Lexing;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Preprocessing;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Parsing;

/// <summary>
/// Builds a <see cref="TexinfoDocument"/> from a preprocessed token stream. Everything the
/// preprocessor handles - includes, conditionals, values, macros, comments and raw output blocks
/// - is already gone by the time the parser runs, so this stage sees only structure: sectioning
/// commands, block environments, paragraphs and inline markup.
/// </summary>
/// <remarks>
/// The parser never throws for a document problem. Unbalanced environments, unknown commands and
/// malformed arguments all produce a warning and the most useful degraded shape available, on the
/// principle that a manual with one broken line should still render the other ten thousand.
/// </remarks>
internal sealed class TexinfoParser
{
    /// <summary>What ends the block sequence currently being parsed.</summary>
    private sealed class ParseScope
    {
        private ParseScope(string endName, bool stopAtItem, bool stopAtTab, bool isDocument)
        {
            EndName = endName;
            StopAtItem = stopAtItem;
            StopAtTab = stopAtTab;
            IsDocument = isDocument;
        }

        /// <summary>The document's outermost scope, where sectioning commands are handled.</summary>
        public static readonly ParseScope Document = new ParseScope(null, false, false, true);

        /// <summary>Creates the scope for an environment body ended by <c>@end name</c>.</summary>
        public static ParseScope Environment(string endName)
            => new ParseScope(endName, false, false, false);

        /// <summary>Creates the scope for a list item or table entry, also ended by the next item.</summary>
        public static ParseScope Item(string endName) => new ParseScope(endName, true, false, false);

        /// <summary>Creates the scope for a multitable cell, also ended by <c>@tab</c>.</summary>
        public static ParseScope Cell(string endName) => new ParseScope(endName, true, true, false);

        public string EndName { get; }

        public bool StopAtItem { get; }

        public bool StopAtTab { get; }

        public bool IsDocument { get; }
    }

    /// <summary>
    /// Collects blocks into one destination list while keeping the current paragraph open across
    /// tokens, so inline content accumulates until something structural closes it.
    /// </summary>
    private sealed class BlockBuilder
    {
        private readonly TexinfoParser _parser;
        private List<TexinfoNode> _paragraph;
        private SourcePosition _paragraphStart;
        private ParagraphAlignment _alignment;
        private bool _suppressIndent;
        private bool _noIndentPending;

        public BlockBuilder(TexinfoParser parser, List<TexinfoNode> target)
        {
            _parser = parser;
            Target = target;
        }

        /// <summary>Where finished blocks go; the document loop swaps this as sections start.</summary>
        public List<TexinfoNode> Target { get; set; }

        /// <summary>True while a paragraph is accumulating content.</summary>
        public bool HasOpenParagraph => _paragraph != null;

        /// <summary>Returns the open paragraph, starting one if none is open.</summary>
        public List<TexinfoNode> Paragraph(SourcePosition position)
        {
            if (_paragraph == null)
            {
                _parser.FlushPendingNode(Target);
                _paragraph = new List<TexinfoNode>();
                _paragraphStart = position;
                _suppressIndent = _noIndentPending;
                _noIndentPending = false;
            }
            return _paragraph;
        }

        /// <summary>Adds a line break to the open paragraph, if there is one.</summary>
        public void AppendNewline(SourcePosition position)
        {
            if (_paragraph != null)
            {
                _paragraph.Add(new TextNode("\n", position));
            }
        }

        /// <summary>Closes the open paragraph, discarding it when it holds nothing but whitespace.</summary>
        public void FlushParagraph()
        {
            if (_paragraph == null)
            {
                return;
            }
            List<TexinfoNode> content = _paragraph;
            _paragraph = null;
            if (InlineNodes.HasVisibleContent(content))
            {
                Target.Add(new ParagraphNode(InlineNodes.Trim(content), _alignment, _suppressIndent,
                    _paragraphStart));
            }
            _alignment = ParagraphAlignment.Default;
            _suppressIndent = false;
        }

        /// <summary>Closes the open paragraph and appends a finished block.</summary>
        public void AddBlock(TexinfoNode node)
        {
            FlushParagraph();
            _parser.FlushPendingNode(Target);
            Target.Add(node);
        }

        /// <summary>Records that the next paragraph starts without an indent.</summary>
        public void RequestNoIndent()
        {
            FlushParagraph();
            _noIndentPending = true;
        }
    }

    private readonly PreprocessedDocument _input;
    private readonly TexinfoWarningCollection _warnings;
    private readonly IReadOnlyList<TexinfoToken> _tokens;
    private readonly TexinfoDocument _document;
    private readonly List<string> _openEnvironments = new List<string>();
    private readonly List<SectionNode> _sectionStack = new List<SectionNode>();
    private readonly Dictionary<string, string> _userIndexCommands =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private int _index;
    private long _steps;
    private TexinfoToken _pushback;
    private bool _sawBye;
    private SectionNode _currentSection;
    private NodeAnchorNode _pendingNode;
    private TexinfoAnchor _pendingAnchor;
    private DefinitionNode _currentDefinition;
    private List<TexinfoNode> _definitionBlockTarget;
    private FloatNode _currentFloat;
    private Dictionary<string, List<int>> _endCommandPositions;
    private int _sectionLevelOffset;
    private string _definitionPending = string.Empty;
    private SourcePosition _definitionPendingPosition;

    /// <summary>Creates a parser over a preprocessed document. One instance parses one document.</summary>
    /// <param name="input">The preprocessor's output; its warning collection is reused.</param>
    public TexinfoParser(PreprocessedDocument input)
    {
        _input = input;
        _warnings = input.Warnings;
        _tokens = input.Tokens;
        _document = new TexinfoDocument(_warnings);
    }

    /// <summary>Parses the token stream and returns the document tree.</summary>
    public TexinfoDocument Parse()
    {
        _document.Encoding = _input.DocumentEncoding;
        _document.Values = _input.Values;
        SkipLeadingTexInputLine();
        ParseBlockSequence(ParseScope.Document, _document.Preamble);
        return _document;
    }

    /// <summary>
    /// Drops the <c>\input texinfo</c> line that every Texinfo file opens with. It is an
    /// instruction to TeX rather than content, and it is not an <c>@</c>-command, so without this
    /// it would be rendered as the document's first paragraph.
    /// </summary>
    private void SkipLeadingTexInputLine()
    {
        int saved = _index;
        while (Peek().Kind == TexinfoTokenKind.Newline)
        {
            Advance();
        }
        TexinfoToken token = Peek();
        string text = token.Value.TrimStart();
        if (token.Kind == TexinfoTokenKind.Text
            && token.AtLineStart
            && text.StartsWith("\\input", StringComparison.Ordinal)
            && text.Contains("texinfo", StringComparison.Ordinal))
        {
            SkipRestOfLine();
            return;
        }
        _index = saved;
    }

    // ----- Token cursor ------------------------------------------------------------------------

    private TexinfoToken Peek()
    {
        if (_pushback != null)
        {
            return _pushback;
        }
        return _index < _tokens.Count ? _tokens[_index] : _tokens[_tokens.Count - 1];
    }

    private void Advance()
    {
        _steps++;
        if (_pushback != null)
        {
            _pushback = null;
            return;
        }
        if (_index < _tokens.Count)
        {
            _index++;
        }
    }

    /// <summary>
    /// Returns the unconsumed tail of a text token to the stream. The braceless accent commands
    /// are why this exists: <c>@'e</c> takes one character of the text that follows it and the
    /// rest of that token is still the document's.
    /// </summary>
    private void PushBackText(string text, SourcePosition position)
    {
        //The one slot is enough because a pushback is always consumed before the next one is made;
        //discarding a live one would silently lose text, so it is worth being loud about.
        if (_pushback != null)
        {
            Warn(TexinfoWarningCategory.Syntax, position,
                "Internal parser error: two token pushbacks at once; some text was dropped.");
        }
        _pushback = new TexinfoToken(TexinfoTokenKind.Text, text, position, atLineStart: false);
    }

    private void Warn(TexinfoWarningCategory category, SourcePosition position, string message)
        => _warnings.Add(category, position, message);

    private string ReadRawLine()
    {
        StringBuilder builder = new StringBuilder();
        while (true)
        {
            TexinfoToken token = Peek();
            if (token.Kind == TexinfoTokenKind.EndOfInput)
            {
                break;
            }
            Advance();
            if (token.Kind == TexinfoTokenKind.Newline)
            {
                break;
            }
            builder.Append(token.ToSourceText());
        }
        return builder.ToString().Trim();
    }

    private void SkipRestOfLine() => ReadRawLine();

    private static bool IsWhitespaceOnly(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }
        return true;
    }

    // ----- Block sequences ---------------------------------------------------------------------

    private void ParseBlockSequence(ParseScope scope, List<TexinfoNode> sink)
    {
        BlockBuilder builder = new BlockBuilder(this, sink);
        while (!_sawBye)
        {
            TexinfoToken token = Peek();
            if (IsScopeTerminator(token, scope))
            {
                break;
            }
            if (scope.IsDocument
                && token.Kind == TexinfoTokenKind.Command
                && token.AtLineStart
                && TexinfoCommandTable.IsSectioning(token.Value))
            {
                builder.FlushParagraph();
                builder.Target = StartSection(token);
                continue;
            }
            long before = _steps;
            ParseBlockToken(token, builder, scope);
            if (_steps == before)
            {
                Warn(TexinfoWarningCategory.Syntax, token.Position,
                    $"Unexpected {token.Kind} token in the document body; it was skipped.");
                Advance();
            }
        }
        builder.FlushParagraph();
        FlushPendingNode(builder.Target);
    }

    private bool IsScopeTerminator(TexinfoToken token, ParseScope scope)
    {
        switch (token.Kind)
        {
            case TexinfoTokenKind.EndOfInput:
                return true;
            case TexinfoTokenKind.EndCommand:
                if (scope.EndName != null && string.Equals(token.Value, scope.EndName, StringComparison.Ordinal))
                {
                    return true;
                }
                return _openEnvironments.Contains(token.Value);
            case TexinfoTokenKind.Command:
                if (scope.StopAtTab && token.Value == "tab")
                {
                    return true;
                }
                return scope.StopAtItem
                       && token.AtLineStart
                       && (token.Value == "item" || token.Value == "itemx" || token.Value == "headitem");
            default:
                return false;
        }
    }

    private void ParseBlockToken(TexinfoToken token, BlockBuilder builder, ParseScope scope)
    {
        switch (token.Kind)
        {
            case TexinfoTokenKind.Newline:
                Advance();
                if (token.AtLineStart)
                {
                    builder.FlushParagraph();
                }
                else
                {
                    builder.AppendNewline(token.Position);
                }
                return;

            case TexinfoTokenKind.Text:
                Advance();
                if (!builder.HasOpenParagraph && IsWhitespaceOnly(token.Value))
                {
                    return;
                }
                builder.Paragraph(token.Position).Add(new TextNode(token.Value, token.Position));
                return;

            case TexinfoTokenKind.OpenBrace:
            case TexinfoTokenKind.CloseBrace:
                Advance();
                builder.Paragraph(token.Position).Add(new TextNode(
                    token.Kind == TexinfoTokenKind.OpenBrace ? "{" : "}", token.Position));
                return;

            case TexinfoTokenKind.RawBlock:
                Advance();
                // A brace-form music snippet is only inline when it really sits inside running
                // text; one written on a line of its own is a display block like the @end form.
                if (token.IsBraceRawBlock && (builder.HasOpenParagraph || !token.AtLineStart))
                {
                    builder.Paragraph(token.Position).Add(BuildRawBlockNode(token, inline: true));
                }
                else
                {
                    builder.AddBlock(BuildRawBlockNode(token, inline: false));
                }
                return;

            case TexinfoTokenKind.EndCommand:
                // A stray '@end' still ends the paragraph: it is a structural command, and every
                // other structural command at this level closes what was open.
                Warn(TexinfoWarningCategory.Syntax, token.Position,
                    $"'@end {token.Value}' does not match an open environment; it was ignored.");
                builder.FlushParagraph();
                Advance();
                SkipRestOfLine();
                return;

            case TexinfoTokenKind.Command:
                ParseCommandInBlock(token, builder, scope);
                return;

            default:
                Advance();
                return;
        }
    }

    private TexinfoNode BuildRawBlockNode(TexinfoToken token, bool inline)
    {
        if (token.Value == "verb")
        {
            //@verb is always inline, however its line was laid out: the lexer captured its text
            //between the delimiters the author chose, and every character of it is literal.
            List<TexinfoNode> verbatim = new List<TexinfoNode>
            {
                new TextNode(token.RawContent, token.Position)
            };
            return new InlineCommandNode("verb", InlineStyle.Code, verbatim, token.Position);
        }
        if (token.Value == "verbatim")
        {
            if (!inline)
            {
                return new VerbatimNode(token.RawContent, token.Position);
            }
            List<TexinfoNode> content = new List<TexinfoNode> { new TextNode(token.RawContent, token.Position) };
            return new InlineCommandNode("verbatim", InlineStyle.Code, content, token.Position);
        }
        return new MusicSnippetNode(token.Value, token.RawArgument, token.RawContent,
            token.IsBraceRawBlock, token.Position);
    }

    // ----- Commands at block level -------------------------------------------------------------

    private void ParseCommandInBlock(TexinfoToken token, BlockBuilder builder, ParseScope scope)
    {
        string name = token.Value;

        if (name.Length == 1 && !char.IsLetter(name[0]))
        {
            Advance();
            ParseSingleCharacterCommand(token, builder.Paragraph(token.Position), scope);
            return;
        }

        if (TexinfoCommandTable.TryGetBlockEnvironment(name, out TexinfoBlockKind blockKind,
                out bool isPreformatted))
        {
            ParseEnvironment(token, blockKind, isPreformatted, builder);
            return;
        }

        if (DefinitionCommandTable.TryGetEnvironment(name,
                out DefinitionCommandTable.DefinitionShape definition))
        {
            ParseDefinition(token, definition, builder);
            return;
        }

        if (DefinitionCommandTable.TryGetContinuation(name,
                out DefinitionCommandTable.DefinitionShape continued))
        {
            ParseDefinitionContinuation(token, continued, builder);
            return;
        }

        if (DefinitionCommandTable.TryGetBlockLine(name,
                out DefinitionCommandTable.DefinitionShape blockLine))
        {
            ParseDefinitionLine(token, blockLine, builder);
            return;
        }

        if (_userIndexCommands.TryGetValue(name, out string userIndexName))
        {
            Advance();
            AddInlineOrBlock(builder, BuildIndexEntry(token, userIndexName, scope), token.Position);
            return;
        }

        switch (name)
        {
            case "defblock":
                ParseDefinitionBlock(token, builder);
                return;
            case "float":
                ParseFloat(token, builder);
                return;
            case "caption":
            case "shortcaption":
                ParseCaption(token, builder, scope);
                return;
            case "listoffloats":
                Advance();
                builder.AddBlock(new DirectiveNode(DirectiveKind.ListOfFloats, name, ReadRawLine(),
                    token.Position));
                return;
            case "exdent":
                Advance();
                builder.AddBlock(new ParagraphNode(ParseRestOfLine(scope), ParagraphAlignment.Exdented,
                    suppressIndent: true, token.Position));
                return;
            case "defindex":
            case "defcodeindex":
                ParseIndexDefinition(token);
                return;
            case "raisesections":
            case "lowersections":
                //The shift applies to every sectioning command after it, so it is a parser state
                //change rather than a setting the emitter could act on later.
                Advance();
                SkipRestOfLine();
                _sectionLevelOffset += name == "raisesections" ? -1 : 1;
                return;
            case "shorttitlepage":
                ParseShortTitlePage(token, builder, scope);
                return;
            case "itemize":
            case "enumerate":
                ParseList(token, builder);
                return;
            case "table":
            case "ftable":
            case "vtable":
                ParseTable(token, builder);
                return;
            case "multitable":
                ParseMultitable(token, builder);
                return;
            case "menu":
                builder.AddBlock(ParseMenu(token, isDetailed: false));
                return;
            case "detailmenu":
                builder.AddBlock(ParseMenu(token, isDetailed: true));
                return;
            case "direntry":
            case "nodedescriptionblock":
                //Both describe the document for a menu, and a printed document has no menus.
                SkipEnvironment(token);
                return;
            case "copying":
                ParseCopying(token, builder);
                return;
            case "node":
                ParseNode(token, builder);
                return;
            case "anchor":
                Advance();
                AddInlineOrBlock(builder, BuildAnchor(token, scope), token.Position);
                return;
            case "bye":
                Advance();
                SkipRestOfLine();
                _sawBye = true;
                return;
            case "noindent":
                Advance();
                builder.RequestNoIndent();
                return;
            case "indent":
                Advance();
                return;
            case "center":
                Advance();
                builder.AddBlock(new ParagraphNode(ParseRestOfLine(scope), ParagraphAlignment.Centered,
                    suppressIndent: true, token.Position));
                return;
            case "settitle":
                Advance();
                _document.Title = InlineNodes.ToPlainText(ParseRestOfLine(scope)).Trim();
                return;
            case "documentlanguage":
                Advance();
                _document.Language = ReadRawLine();
                return;
            case "printindex":
                Advance();
                builder.AddBlock(new DirectiveNode(DirectiveKind.PrintIndex, name, ReadRawLine(),
                    token.Position));
                return;
            case "syncodeindex":
            case "synindex":
                ParseIndexMerge(token);
                return;
            case "contents":
                Advance();
                SkipRestOfLine();
                builder.AddBlock(new DirectiveNode(DirectiveKind.Contents, name, string.Empty, token.Position));
                return;
            case "shortcontents":
            case "summarycontents":
                Advance();
                SkipRestOfLine();
                builder.AddBlock(new DirectiveNode(DirectiveKind.ShortContents, name, string.Empty,
                    token.Position));
                return;
            case "insertcopying":
                Advance();
                SkipRestOfLine();
                builder.AddBlock(new DirectiveNode(DirectiveKind.InsertCopying, name, string.Empty,
                    token.Position));
                return;
            case "page":
                Advance();
                SkipRestOfLine();
                builder.AddBlock(new DirectiveNode(DirectiveKind.PageBreak, name, string.Empty, token.Position));
                return;
            case "sp":
            case "vskip":
                Advance();
                builder.AddBlock(new DirectiveNode(DirectiveKind.VerticalSpace, name, ReadRawLine(),
                    token.Position));
                return;
            case "need":
                Advance();
                builder.AddBlock(new DirectiveNode(DirectiveKind.NeedSpace, name, ReadRawLine(), token.Position));
                return;
            case "dircategory":
                Advance();
                SkipRestOfLine();
                return;
            case "item":
            case "itemx":
            case "headitem":
            case "tab":
                Warn(TexinfoWarningCategory.Syntax, token.Position,
                    $"'@{name}' appears outside a list or table; its line was kept as a paragraph.");
                Advance();
                foreach (TexinfoNode node in ParseRestOfLine(scope))
                {
                    builder.Paragraph(token.Position).Add(node);
                }
                return;
            case "columnfractions":
                Warn(TexinfoWarningCategory.Syntax, token.Position,
                    "'@columnfractions' appears outside a '@multitable'; it was ignored.");
                Advance();
                SkipRestOfLine();
                return;
            case "verbatiminclude":
                Advance();
                Warn(TexinfoWarningCategory.Include, token.Position,
                    $"'@verbatiminclude {ReadRawLine()}' could not be resolved; the block is missing.");
                return;
        }

        if (TexinfoCommandTable.TryGetHeading(name, out HeadingKind headingKind, out int headingLevel))
        {
            Advance();
            HeadingNode heading = new HeadingNode(name, headingKind, headingLevel,
                ParseRestOfLine(scope), token.Position);
            if (headingKind == HeadingKind.Author && _document.Author.Length == 0)
            {
                _document.Author = InlineNodes.ToPlainText(heading.Content).Trim();
            }
            builder.AddBlock(heading);
            return;
        }

        if (TexinfoCommandTable.TryGetIndexName(name, out string indexName))
        {
            Advance();
            AddInlineOrBlock(builder, BuildIndexEntry(token, indexName, scope), token.Position);
            return;
        }

        if (TexinfoCommandTable.IsRecordedSetting(name))
        {
            Advance();
            _document.Settings[name] = ReadRawLine();
            return;
        }

        if (TexinfoCommandTable.IsSectioning(name))
        {
            // Only reachable inside an environment, where a sectioning command is not allowed.
            TexinfoCommandTable.TryGetSectioning(name, out int rank, out _);
            int level = ApplySectionLevelOffset(rank);
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{name}' cannot open a section inside an environment; it was rendered as a heading.");
            Advance();
            builder.AddBlock(new HeadingNode(name, LevelToHeadingKind(level), level, ParseRestOfLine(scope),
                token.Position));
            return;
        }

        //An unrecognized command that starts a line and has a matching '@end' further on is an
        //environment this library does not implement. Parsing it as one keeps its content, and
        //keeps its '@end' from being reported separately as a stray.
        if (token.AtLineStart && HasMatchingEndCommand(name))
        {
            ParseGenericEnvironment(token, builder);
            return;
        }

        Advance();
        ParseInlineCommandToken(token, builder.Paragraph(token.Position), scope);
    }

    /// <summary>
    /// True when an <c>@end</c> for the given name appears later in the stream, and so when an
    /// unrecognized command of that name is opening an environment rather than standing alone.
    /// The positions are indexed on first use so a document full of unknown commands cannot turn
    /// this into a scan of the whole stream per command.
    /// </summary>
    private bool HasMatchingEndCommand(string name)
    {
        if (_endCommandPositions == null)
        {
            _endCommandPositions = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].Kind != TexinfoTokenKind.EndCommand)
                {
                    continue;
                }
                if (!_endCommandPositions.TryGetValue(_tokens[i].Value, out List<int> known))
                {
                    known = new List<int>();
                    _endCommandPositions[_tokens[i].Value] = known;
                }
                known.Add(i);
            }
        }
        if (!_endCommandPositions.TryGetValue(name, out List<int> positions))
        {
            return false;
        }
        foreach (int position in positions)
        {
            if (position > _index)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Applies the shift <c>@raisesections</c> and <c>@lowersections</c> asked for. <c>@top</c>
    /// and <c>@part</c> sit above the shiftable range and stay where they are, and the result is
    /// clamped so that no amount of shifting can turn a chapter into the document's top node or
    /// push a subsection past the deepest level there is.
    /// </summary>
    private int ApplySectionLevelOffset(int level)
    {
        if (level < 1 || _sectionLevelOffset == 0)
        {
            return level;
        }
        int shifted = level + _sectionLevelOffset;
        return shifted < 1 ? 1 : shifted > 5 ? 5 : shifted;
    }

    private static HeadingKind LevelToHeadingKind(int level)
    {
        switch (level)
        {
            case 0:
            case 1:
                return HeadingKind.Chapter;
            case 2:
                return HeadingKind.Section;
            case 3:
                return HeadingKind.Subsection;
            default:
                return HeadingKind.Subsubsection;
        }
    }

    private void AddInlineOrBlock(BlockBuilder builder, TexinfoNode node, SourcePosition position)
    {
        if (builder.HasOpenParagraph)
        {
            builder.Paragraph(position).Add(node);
        }
        else
        {
            builder.AddBlock(node);
        }
    }

    // ----- Sections and nodes ------------------------------------------------------------------

    private List<TexinfoNode> StartSection(TexinfoToken token)
    {
        string name = token.Value;
        Advance();
        List<TexinfoNode> title = ParseRestOfLine(ParseScope.Document);
        TexinfoCommandTable.TryGetSectioning(name, out int level, out SectionKind kind);
        level = ApplySectionLevelOffset(level);

        string nodeName = _pendingNode != null ? _pendingNode.NodeName : string.Empty;
        SectionNode section = new SectionNode(name, level, kind, title, nodeName, token.Position);
        if (_pendingAnchor != null)
        {
            _pendingAnchor.Target = section;
        }
        _pendingNode = null;
        _pendingAnchor = null;

        while (_sectionStack.Count > 0 && _sectionStack[_sectionStack.Count - 1].Level >= level)
        {
            _sectionStack.RemoveAt(_sectionStack.Count - 1);
        }
        if (_sectionStack.Count == 0)
        {
            _document.Sections.Add(section);
        }
        else
        {
            _sectionStack[_sectionStack.Count - 1].Children.Add(section);
        }
        _sectionStack.Add(section);
        _currentSection = section;
        return section.Blocks;
    }

    private void ParseNode(TexinfoToken token, BlockBuilder builder)
    {
        Advance();
        List<List<TexinfoNode>> parts = InlineNodes.SplitOnCommas(ParseRestOfLine(ParseScope.Document), 4);
        string nodeName = InlineNodes.PartName(parts, 0);
        NodeAnchorNode node = new NodeAnchorNode(nodeName, InlineNodes.PartText(parts, 1),
            InlineNodes.PartText(parts, 2), InlineNodes.PartText(parts, 3), token.Position);

        builder.FlushParagraph();
        FlushPendingNode(builder.Target);
        _pendingNode = node;
        _pendingAnchor = RegisterAnchor(nodeName, TexinfoAnchorKind.Node, node, token.Position);
    }

    private void FlushPendingNode(List<TexinfoNode> target)
    {
        if (_pendingNode == null)
        {
            return;
        }
        target.Add(_pendingNode);
        _pendingNode = null;
        _pendingAnchor = null;
    }

    private TexinfoAnchor RegisterAnchor(string name, TexinfoAnchorKind kind, TexinfoNode target,
        SourcePosition position)
    {
        if (name.Length == 0)
        {
            Warn(TexinfoWarningCategory.Reference, position, $"A '@{kind}' destination has no name.");
            return null;
        }
        if (_document.Anchors.ContainsKey(name))
        {
            Warn(TexinfoWarningCategory.Reference, position,
                $"'{name}' is defined more than once; the first definition is kept.");
            return null;
        }
        TexinfoAnchor anchor = new TexinfoAnchor(name, kind, target, position);
        _document.Anchors[name] = anchor;
        return anchor;
    }

    // ----- Environments ------------------------------------------------------------------------

    private void ParseEnvironment(TexinfoToken token, TexinfoBlockKind kind, bool isPreformatted,
        BlockBuilder builder)
    {
        string name = token.Value;
        Advance();
        List<TexinfoNode> argument = ParseRestOfLine(ParseScope.Document);
        _openEnvironments.Add(name);
        TexinfoNode node;
        if (isPreformatted)
        {
            node = new PreformattedNode(name, kind, ParsePreformattedContent(name), token.Position);
        }
        else
        {
            List<TexinfoNode> blocks = new List<TexinfoNode>();
            ParseBlockSequence(ParseScope.Environment(name), blocks);
            node = new BlockEnvironmentNode(name, kind, argument, blocks, token.Position);
        }
        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        builder.AddBlock(node);
    }

    private void ParseGenericEnvironment(TexinfoToken token, BlockBuilder builder)
    {
        string name = token.Value;
        Warn(TexinfoWarningCategory.UnknownCommand, token.Position,
            $"'@{name}' is not supported; its content was kept as a plain block.");
        Advance();
        List<TexinfoNode> argument = ParseRestOfLine(ParseScope.Document);
        _openEnvironments.Add(name);
        List<TexinfoNode> blocks = new List<TexinfoNode>();
        ParseBlockSequence(ParseScope.Environment(name), blocks);
        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        builder.AddBlock(new BlockEnvironmentNode(name, TexinfoBlockKind.Unknown, argument, blocks,
            token.Position));
    }

    // ----- Definitions -------------------------------------------------------------------------

    private void ParseDefinition(TexinfoToken token, DefinitionCommandTable.DefinitionShape shape,
        BlockBuilder builder)
    {
        string name = token.Value;
        Advance();
        List<DefinitionHeaderNode> headers = new List<DefinitionHeaderNode>
        {
            ReadDefinitionHeader(token, shape)
        };
        List<TexinfoNode> blocks = new List<TexinfoNode>();
        DefinitionNode node = new DefinitionNode(name, headers, blocks, token.Position);

        _openEnvironments.Add(name);
        DefinitionNode outerDefinition = _currentDefinition;
        List<TexinfoNode> outerBlockTarget = _definitionBlockTarget;
        _currentDefinition = node;
        _definitionBlockTarget = null;
        ParseBlockSequence(ParseScope.Environment(name), blocks);
        _currentDefinition = outerDefinition;
        _definitionBlockTarget = outerBlockTarget;
        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        builder.AddBlock(node);
    }

    /// <summary>
    /// Handles an <c>x</c> form such as <c>@deffnx</c>. Texinfo calls these further 'first' lines,
    /// so however far down the body one is written it heads the definition already open.
    /// </summary>
    private void ParseDefinitionContinuation(TexinfoToken token,
        DefinitionCommandTable.DefinitionShape shape, BlockBuilder builder)
    {
        Advance();
        DefinitionHeaderNode header = ReadDefinitionHeader(token, shape);
        if (_currentDefinition != null)
        {
            _currentDefinition.AddHeader(header);
            return;
        }
        Warn(TexinfoWarningCategory.Syntax, token.Position,
            $"'@{token.Value}' continues a definition that was never opened; its heading line was "
            + "rendered on its own.");
        builder.AddBlock(new DefinitionNode(token.Value,
            new List<DefinitionHeaderNode> { header }, new List<TexinfoNode>(), token.Position));
    }

    /// <summary>
    /// Handles <c>@defline</c> and <c>@deftypeline</c>, the heading lines of a <c>@defblock</c>.
    /// Lines written one after another head one description; the first line after a description
    /// has started begins the next definition, which is the grouping <c>@defblock</c> exists for.
    /// </summary>
    private void ParseDefinitionLine(TexinfoToken token,
        DefinitionCommandTable.DefinitionShape shape, BlockBuilder builder)
    {
        Advance();
        //The paragraph still open is the previous definition's description, and whether that
        //definition has one is exactly what decides whether this line heads it or begins the next.
        builder.FlushParagraph();
        DefinitionHeaderNode header = ReadDefinitionHeader(token, shape);
        if (_currentDefinition != null && _currentDefinition.Blocks.Count == 0)
        {
            _currentDefinition.AddHeader(header);
            return;
        }
        if (_definitionBlockTarget == null)
        {
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{token.Value}' appears outside a '@defblock'; it was rendered on its own.");
        }
        List<TexinfoNode> blocks = new List<TexinfoNode>();
        DefinitionNode node = new DefinitionNode(token.Value,
            new List<DefinitionHeaderNode> { header }, blocks, token.Position);
        if (_definitionBlockTarget != null)
        {
            builder.Target = _definitionBlockTarget;
        }
        builder.AddBlock(node);
        builder.Target = blocks;
        _currentDefinition = node;
    }

    private void ParseDefinitionBlock(TexinfoToken token, BlockBuilder builder)
    {
        const string name = "defblock";
        Advance();
        SkipRestOfLine();
        List<TexinfoNode> definitions = new List<TexinfoNode>();

        _openEnvironments.Add(name);
        DefinitionNode outerDefinition = _currentDefinition;
        List<TexinfoNode> outerBlockTarget = _definitionBlockTarget;
        _currentDefinition = null;
        _definitionBlockTarget = definitions;
        ParseBlockSequence(ParseScope.Environment(name), definitions);
        _currentDefinition = outerDefinition;
        _definitionBlockTarget = outerBlockTarget;
        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        builder.AddBlock(new BlockEnvironmentNode(name, TexinfoBlockKind.DefinitionBlock,
            InlineNodes.None, definitions, token.Position));
    }

    /// <summary>
    /// Reads one definition heading line: a run of words - a braced group counting as one - laid
    /// out as category, class, data type and name, with everything left on the line becoming the
    /// arguments. A command that supplies its own category reads one word fewer.
    /// </summary>
    private DefinitionHeaderNode ReadDefinitionHeader(TexinfoToken token,
        DefinitionCommandTable.DefinitionShape shape)
    {
        _definitionPending = string.Empty;
        _definitionPendingPosition = token.Position;
        List<TexinfoNode> category = shape.FixedCategory.Length > 0
            ? new List<TexinfoNode> { new TextNode(shape.FixedCategory, token.Position) }
            : ReadDefinitionWord();
        List<TexinfoNode> className = shape.HasClass ? ReadDefinitionWord() : new List<TexinfoNode>();
        List<TexinfoNode> dataType = shape.HasDataType ? ReadDefinitionWord() : new List<TexinfoNode>();
        List<TexinfoNode> entityName = ReadDefinitionWord();
        List<TexinfoNode> arguments = ReadDefinitionArguments();
        if (entityName.Count == 0)
        {
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{token.Value}' names no entity; its heading line was rendered as written.");
        }
        return new DefinitionHeaderNode(token.Value, category, className, dataType, entityName,
            arguments, shape.ClassPreposition, shape.HasDataType, token.Position)
        {
            IndexEntry = BuildDefinitionIndexEntry(shape, entityName, className, token.Position)
        };
    }

    /// <summary>
    /// Reads one word of a definition heading line. A braced group is one word however many
    /// spaces it holds, which is how a category such as <c>{Special Form}</c> is written.
    /// </summary>
    private List<TexinfoNode> ReadDefinitionWord()
    {
        SkipDefinitionSpaces();
        List<TexinfoNode> word = new List<TexinfoNode>();
        while (true)
        {
            if (_definitionPending.Length > 0)
            {
                int end = IndexOfWhitespace(_definitionPending);
                if (end < 0)
                {
                    word.Add(new TextNode(_definitionPending, _definitionPendingPosition));
                    _definitionPending = string.Empty;
                    continue;
                }
                if (end > 0)
                {
                    word.Add(new TextNode(_definitionPending.Substring(0, end),
                        _definitionPendingPosition));
                }
                _definitionPending = _definitionPending.Substring(end);
                break;
            }

            TexinfoToken token = Peek();
            if (token.Kind == TexinfoTokenKind.Newline
                || token.Kind == TexinfoTokenKind.EndOfInput
                || token.Kind == TexinfoTokenKind.EndCommand)
            {
                break;
            }
            if (token.Kind == TexinfoTokenKind.Text)
            {
                Advance();
                _definitionPending = token.Value;
                _definitionPendingPosition = token.Position;
                continue;
            }
            if (token.Kind == TexinfoTokenKind.OpenBrace)
            {
                Advance();
                word.AddRange(ParseBraceGroup(ParseScope.Document));
                //A group written on its own is the whole word; one written against preceding text
                //belongs to that word, and reading on would swallow the next one.
                if (word.Count > 0)
                {
                    break;
                }
                continue;
            }
            if (token.Kind == TexinfoTokenKind.CloseBrace)
            {
                Advance();
                word.Add(new TextNode("}", token.Position));
                continue;
            }
            //A lone '@' before the line break continues the heading, and the whitespace around it
            //collapses into the single space that separates two words.
            if (token.Kind == TexinfoTokenKind.Command && IsInterwordSpaceCommand(token.Value))
            {
                Advance();
                break;
            }
            Advance();
            if (token.Kind == TexinfoTokenKind.RawBlock)
            {
                word.Add(BuildRawBlockNode(token, inline: true));
                continue;
            }
            ParseInlineCommandToken(token, word, ParseScope.Document);
        }
        return InlineNodes.Trim(word);
    }

    private void SkipDefinitionSpaces()
    {
        while (true)
        {
            if (_definitionPending.Length > 0)
            {
                string remaining = _definitionPending.TrimStart();
                if (remaining.Length > 0)
                {
                    _definitionPending = remaining;
                    return;
                }
                _definitionPending = string.Empty;
            }
            TexinfoToken token = Peek();
            if (token.Kind == TexinfoTokenKind.Text)
            {
                Advance();
                _definitionPending = token.Value;
                _definitionPendingPosition = token.Position;
                continue;
            }
            if (token.Kind == TexinfoTokenKind.Command && IsInterwordSpaceCommand(token.Value))
            {
                Advance();
                continue;
            }
            return;
        }
    }

    private List<TexinfoNode> ReadDefinitionArguments()
    {
        List<TexinfoNode> arguments = new List<TexinfoNode>();
        if (_definitionPending.Length > 0)
        {
            arguments.Add(new TextNode(_definitionPending, _definitionPendingPosition));
            _definitionPending = string.Empty;
        }
        arguments.AddRange(ParseRestOfLine(ParseScope.Document));
        return InlineNodes.Trim(arguments);
    }

    private IndexEntryNode BuildDefinitionIndexEntry(DefinitionCommandTable.DefinitionShape shape,
        List<TexinfoNode> entityName, List<TexinfoNode> className, SourcePosition position)
    {
        if (shape.IndexName.Length == 0 || entityName.Count == 0)
        {
            return null;
        }
        //An entry for something that belongs to a class names the class too: two classes routinely
        //define a member of the same name, and an index of bare names could not tell them apart.
        List<TexinfoNode> content = new List<TexinfoNode>(entityName);
        if (className.Count > 0 && shape.ClassPreposition.Length > 0)
        {
            content.Add(new TextNode(" " + shape.ClassPreposition + " ", position));
            content.AddRange(className);
        }
        IndexEntryNode entry = new IndexEntryNode(shape.IndexName, shape.CommandName, content,
            string.Empty, position)
        {
            Section = _currentSection
        };
        _document.IndexEntries.Add(entry);
        return entry;
    }

    private static bool IsInterwordSpaceCommand(string name)
        => name == " " || name == "\t" || name == "\n";

    private static int IndexOfWhitespace(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }
        return -1;
    }

    // ----- Floats ------------------------------------------------------------------------------

    private void ParseFloat(TexinfoToken token, BlockBuilder builder)
    {
        const string name = "float";
        Advance();
        List<List<TexinfoNode>> parts =
            InlineNodes.SplitOnCommas(ParseRestOfLine(ParseScope.Document), 2);
        List<TexinfoNode> blocks = new List<TexinfoNode>();
        FloatNode node = new FloatNode(InlineNodes.PartText(parts, 0), InlineNodes.PartName(parts, 1),
            blocks, token.Position);
        if (node.Label.Length > 0)
        {
            RegisterAnchor(node.Label, TexinfoAnchorKind.Float, node, token.Position);
        }
        _document.Floats.Add(node);

        _openEnvironments.Add(name);
        FloatNode outerFloat = _currentFloat;
        _currentFloat = node;
        ParseBlockSequence(ParseScope.Environment(name), blocks);
        _currentFloat = outerFloat;
        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        builder.AddBlock(node);
    }

    private void ParseCaption(TexinfoToken token, BlockBuilder builder, ParseScope scope)
    {
        Advance();
        List<TexinfoNode> content = InlineNodes.Trim(ParseOptionalBraceGroup(scope));
        if (_currentFloat == null)
        {
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{token.Value}' appears outside a '@float'; its text was kept as a paragraph.");
            builder.AddBlock(new ParagraphNode(content, ParagraphAlignment.Default,
                suppressIndent: true, token.Position));
            return;
        }
        if (token.Value == "shortcaption")
        {
            _currentFloat.ShortCaption = content;
            return;
        }
        _currentFloat.Caption = content;
    }

    // ----- Document-level declarations ---------------------------------------------------------

    /// <summary>
    /// Handles <c>@defindex</c> and <c>@defcodeindex</c>, which create an index and, with it, the
    /// <c>@&lt;name&gt;index</c> command that files entries in it.
    /// </summary>
    private void ParseIndexDefinition(TexinfoToken token)
    {
        Advance();
        string line = ReadRawLine();
        int end = IndexOfWhitespace(line);
        string indexName = end < 0 ? line : line.Substring(0, end);
        if (indexName.Length == 0)
        {
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{token.Value}' has no index name; it was ignored.");
            return;
        }
        _userIndexCommands[indexName + "index"] = indexName;
        if (token.Value == "defcodeindex")
        {
            _document.CodeIndexNames.Add(indexName);
        }
    }

    /// <summary>
    /// Handles <c>@shorttitlepage</c>, which is a title page carrying nothing but the title. It is
    /// built as one so that the emitter hoists and styles it exactly as it does <c>@titlepage</c>.
    /// </summary>
    private void ParseShortTitlePage(TexinfoToken token, BlockBuilder builder, ParseScope scope)
    {
        Advance();
        List<TexinfoNode> blocks = new List<TexinfoNode>
        {
            new HeadingNode(token.Value, HeadingKind.Title, 0, ParseRestOfLine(scope), token.Position)
        };
        builder.AddBlock(new BlockEnvironmentNode(token.Value, TexinfoBlockKind.TitlePage,
            InlineNodes.None, blocks, token.Position));
    }

    private void ParseCopying(TexinfoToken token, BlockBuilder builder)
    {
        builder.FlushParagraph();
        Advance();
        SkipRestOfLine();
        _openEnvironments.Add("copying");
        ParseBlockSequence(ParseScope.Environment("copying"), _document.Copying);
        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd("copying", token.Position);
    }

    private void SkipEnvironment(TexinfoToken token)
    {
        string name = token.Value;
        Advance();
        SkipRestOfLine();
        while (true)
        {
            TexinfoToken current = Peek();
            if (current.Kind == TexinfoTokenKind.EndOfInput)
            {
                Warn(TexinfoWarningCategory.Syntax, token.Position,
                    $"'@{name}' is missing its '@end {name}'.");
                return;
            }
            if (current.Kind == TexinfoTokenKind.EndCommand)
            {
                if (current.Value == name)
                {
                    Advance();
                    SkipRestOfLine();
                    return;
                }
                if (_openEnvironments.Contains(current.Value))
                {
                    Warn(TexinfoWarningCategory.Syntax, token.Position,
                        $"'@{name}' is missing its '@end {name}'.");
                    return;
                }
            }
            Advance();
        }
    }

    private void ExpectEnd(string name, SourcePosition start)
    {
        TexinfoToken token = Peek();
        if (token.Kind == TexinfoTokenKind.EndCommand
            && string.Equals(token.Value, name, StringComparison.Ordinal))
        {
            Advance();
            SkipRestOfLine();
            return;
        }
        Warn(TexinfoWarningCategory.Syntax, start, $"'@{name}' is missing its '@end {name}'.");
    }

    /// <summary>
    /// Parses a preformatted environment written INSIDE another one - <c>@example</c> inside
    /// <c>@display</c>, which is legal Texinfo. It stays a node of its own so that its content
    /// keeps its own literalness (an <c>@example</c> holds code even when the <c>@display</c>
    /// around it holds prose); the emitter renders it as one more step of indentation rather than
    /// as a second preformatted element. An environment that is NOT preformatted is left alone
    /// here: a list or a table cannot be represented inside preformatted text, and saying so with
    /// a warning is better than pretending.
    /// </summary>
    private PreformattedNode ParseNestedPreformatted(TexinfoToken token, TexinfoBlockKind kind)
    {
        string name = token.Value;
        Advance();
        SkipRestOfLine();
        _openEnvironments.Add(name);
        List<TexinfoNode> content = ParsePreformattedContent(name);
        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        return new PreformattedNode(name, kind, content, token.Position);
    }

    private List<TexinfoNode> ParsePreformattedContent(string name)
    {
        List<TexinfoNode> content = new List<TexinfoNode>();
        while (true)
        {
            TexinfoToken token = Peek();
            if (token.Kind == TexinfoTokenKind.EndOfInput)
            {
                break;
            }
            if (token.Kind == TexinfoTokenKind.EndCommand)
            {
                if (token.Value == name)
                {
                    break;
                }
                if (token.Value == "group")
                {
                    Advance();
                    SkipRestOfLine();
                    continue;
                }
                if (_openEnvironments.Contains(token.Value))
                {
                    break;
                }
                Warn(TexinfoWarningCategory.Syntax, token.Position,
                    $"'@end {token.Value}' does not match an open environment; it was ignored.");
                Advance();
                SkipRestOfLine();
                continue;
            }
            if (token.Kind == TexinfoTokenKind.Command && token.AtLineStart
                && (token.Value == "group" || TexinfoCommandTable.IsRecordedSetting(token.Value)))
            {
                // @group inside a preformatted block only asks for page-break protection, and a
                // setting command is a whole line of instruction to the formatter either way.
                Advance();
                if (token.Value != "group")
                {
                    _document.Settings[token.Value] = ReadRawLine();
                    continue;
                }
                SkipRestOfLine();
                continue;
            }
            if (token.Kind == TexinfoTokenKind.Command && token.AtLineStart)
            {
                if (token.Value == "noindent" || token.Value == "indent")
                {
                    //Paragraph indentation says nothing inside preformatted text, where every line
                    //already sits exactly where the source put it. On a line of its own the whole
                    //line goes, so the instruction leaves no blank line behind.
                    Advance();
                    if (Peek().Kind == TexinfoTokenKind.Newline)
                    {
                        Advance();
                    }
                    continue;
                }
                if (TexinfoCommandTable.TryGetBlockEnvironment(token.Value,
                        out TexinfoBlockKind nestedKind, out bool nestedIsPreformatted)
                    && nestedIsPreformatted)
                {
                    content.Add(ParseNestedPreformatted(token, nestedKind));
                    continue;
                }
            }
            Advance();
            switch (token.Kind)
            {
                case TexinfoTokenKind.Newline:
                    content.Add(new TextNode("\n", token.Position));
                    break;
                case TexinfoTokenKind.Text:
                    content.Add(new TextNode(token.Value, token.Position));
                    break;
                case TexinfoTokenKind.OpenBrace:
                    content.Add(new TextNode("{", token.Position));
                    break;
                case TexinfoTokenKind.CloseBrace:
                    content.Add(new TextNode("}", token.Position));
                    break;
                case TexinfoTokenKind.RawBlock:
                    content.Add(BuildRawBlockNode(token, inline: true));
                    break;
                default:
                    ParseInlineCommandToken(token, content, ParseScope.Environment(name));
                    break;
            }
        }
        return content;
    }

    // ----- Lists and tables --------------------------------------------------------------------

    private void ParseList(TexinfoToken token, BlockBuilder builder)
    {
        string name = token.Value;
        Advance();
        string marker = StripCommandMarker(ReadRawLine());
        _openEnvironments.Add(name);
        ParseScope itemScope = ParseScope.Item(name);
        List<ListItemNode> items = new List<ListItemNode>();
        List<TexinfoNode> leading = new List<TexinfoNode>();

        while (true)
        {
            TexinfoToken current = Peek();
            if (current.Kind == TexinfoTokenKind.EndOfInput || current.Kind == TexinfoTokenKind.EndCommand)
            {
                break;
            }
            if (current.Kind == TexinfoTokenKind.Command && current.AtLineStart
                && (current.Value == "item" || current.Value == "itemx" || current.Value == "headitem"))
            {
                Advance();
                List<TexinfoNode> blocks = new List<TexinfoNode>();
                ParseBlockSequence(itemScope, blocks);
                items.Add(new ListItemNode(blocks, current.Position));
                continue;
            }
            if (current.Kind == TexinfoTokenKind.Newline
                || (current.Kind == TexinfoTokenKind.Text && IsWhitespaceOnly(current.Value)))
            {
                Advance();
                continue;
            }
            long before = _steps;
            ParseBlockSequence(itemScope, leading);
            if (_steps == before)
            {
                Advance();
            }
        }

        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        foreach (TexinfoNode node in leading)
        {
            builder.AddBlock(node);
        }
        builder.AddBlock(new ListNode(name == "enumerate", marker, items, token.Position));
    }

    private void ParseTable(TexinfoToken token, BlockBuilder builder)
    {
        string name = token.Value;
        Advance();
        string format = StripCommandMarker(ReadRawLine());
        string indexName = name == "ftable" ? "fn" : name == "vtable" ? "vr" : string.Empty;
        _openEnvironments.Add(name);
        ParseScope itemScope = ParseScope.Item(name);

        List<TableEntryNode> entries = new List<TableEntryNode>();
        List<TableTermNode> terms = new List<TableTermNode>();
        List<TexinfoNode> blocks = new List<TexinfoNode>();
        SourcePosition entryStart = token.Position;

        void CloseEntry()
        {
            if (terms.Count > 0 || blocks.Count > 0)
            {
                entries.Add(new TableEntryNode(terms, blocks, entryStart));
                terms = new List<TableTermNode>();
                blocks = new List<TexinfoNode>();
            }
        }

        while (true)
        {
            TexinfoToken current = Peek();
            if (current.Kind == TexinfoTokenKind.EndOfInput || current.Kind == TexinfoTokenKind.EndCommand)
            {
                break;
            }
            if (current.Kind == TexinfoTokenKind.Command && current.AtLineStart
                && (current.Value == "item" || current.Value == "itemx" || current.Value == "headitem"))
            {
                bool isContinuation = current.Value == "itemx";
                Advance();
                List<TexinfoNode> term = ParseRestOfLine(itemScope);
                if (!isContinuation || blocks.Count > 0 || terms.Count == 0)
                {
                    CloseEntry();
                    entryStart = current.Position;
                }
                TableTermNode termNode = new TableTermNode(term, isContinuation, current.Position);
                //@ftable and @vtable are @table plus an index entry for every term, which is the
                //only thing that distinguishes them.
                if (indexName.Length > 0 && term.Count > 0)
                {
                    IndexEntryNode entry = new IndexEntryNode(indexName, name, term, string.Empty,
                        current.Position)
                    {
                        Section = _currentSection
                    };
                    _document.IndexEntries.Add(entry);
                    termNode.IndexEntry = entry;
                }
                terms.Add(termNode);
                ParseBlockSequence(itemScope, blocks);
                continue;
            }
            if (current.Kind == TexinfoTokenKind.Newline
                || (current.Kind == TexinfoTokenKind.Text && IsWhitespaceOnly(current.Value)))
            {
                Advance();
                continue;
            }
            long before = _steps;
            ParseBlockSequence(itemScope, blocks);
            if (_steps == before)
            {
                Advance();
            }
        }
        CloseEntry();

        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        builder.AddBlock(new TableNode(name, format, indexName, entries, token.Position));
    }

    private void ParseMultitable(TexinfoToken token, BlockBuilder builder)
    {
        const string name = "multitable";
        Advance();
        string specification = ReadRawLine();
        List<double> fractions = new List<double>();
        List<string> prototypes = new List<string>();
        ReadColumnSpecification(specification, fractions, prototypes, token.Position);

        _openEnvironments.Add(name);
        ParseScope cellScope = ParseScope.Cell(name);
        List<MultitableRowNode> rows = new List<MultitableRowNode>();

        while (true)
        {
            TexinfoToken current = Peek();
            if (current.Kind == TexinfoTokenKind.EndOfInput || current.Kind == TexinfoTokenKind.EndCommand)
            {
                break;
            }
            if (current.Kind == TexinfoTokenKind.Command && current.AtLineStart
                && (current.Value == "item" || current.Value == "headitem" || current.Value == "itemx"))
            {
                Advance();
                rows.Add(ParseMultitableRow(current, cellScope));
                continue;
            }
            if (current.Kind == TexinfoTokenKind.Newline
                || (current.Kind == TexinfoTokenKind.Text && IsWhitespaceOnly(current.Value)))
            {
                Advance();
                continue;
            }
            long before = _steps;
            List<TexinfoNode> stray = new List<TexinfoNode>();
            ParseBlockSequence(cellScope, stray);
            if (_steps == before)
            {
                Advance();
            }
        }

        _openEnvironments.RemoveAt(_openEnvironments.Count - 1);
        ExpectEnd(name, token.Position);
        builder.AddBlock(new MultitableNode(fractions, prototypes, rows, token.Position));
    }

    private MultitableRowNode ParseMultitableRow(TexinfoToken token, ParseScope cellScope)
    {
        List<MultitableCellNode> cells = new List<MultitableCellNode>();
        while (true)
        {
            SourcePosition cellStart = Peek().Position;
            List<TexinfoNode> blocks = new List<TexinfoNode>();
            ParseBlockSequence(cellScope, blocks);
            cells.Add(new MultitableCellNode(blocks, cellStart));
            TexinfoToken current = Peek();
            if (current.Kind == TexinfoTokenKind.Command && current.Value == "tab")
            {
                Advance();
                continue;
            }
            break;
        }
        return new MultitableRowNode(token.Value == "headitem", cells, token.Position);
    }

    private void ReadColumnSpecification(string specification, List<double> fractions,
        List<string> prototypes, SourcePosition position)
    {
        const string fractionsCommand = "@columnfractions";
        if (specification.StartsWith(fractionsCommand, StringComparison.Ordinal))
        {
            string[] values = specification.Substring(fractionsCommand.Length)
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string value in values)
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double fraction))
                {
                    fractions.Add(fraction);
                }
                else
                {
                    Warn(TexinfoWarningCategory.Syntax, position,
                        $"'@columnfractions' value '{value}' is not a number; it was ignored.");
                }
            }
            return;
        }
        int index = 0;
        while (index < specification.Length)
        {
            if (specification[index] != '{')
            {
                index++;
                continue;
            }
            int depth = 1;
            int start = ++index;
            while (index < specification.Length && depth > 0)
            {
                if (specification[index] == '{')
                {
                    depth++;
                }
                else if (specification[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
                index++;
            }
            prototypes.Add(specification.Substring(start, Math.Min(index, specification.Length) - start));
            index++;
        }
    }

    private static string StripCommandMarker(string text)
    {
        string trimmed = text.Trim();
        return trimmed.StartsWith("@", StringComparison.Ordinal) ? trimmed.Substring(1) : trimmed;
    }

    // ----- Menus -------------------------------------------------------------------------------

    private MenuNode ParseMenu(TexinfoToken token, bool isDetailed)
    {
        string name = isDetailed ? "detailmenu" : "menu";
        Advance();
        SkipRestOfLine();
        List<MenuEntryNode> entries = new List<MenuEntryNode>();
        while (true)
        {
            TexinfoToken current = Peek();
            if (current.Kind == TexinfoTokenKind.EndOfInput)
            {
                Warn(TexinfoWarningCategory.Syntax, token.Position, $"'@{name}' is missing its '@end {name}'.");
                break;
            }
            if (current.Kind == TexinfoTokenKind.EndCommand)
            {
                if (current.Value == name)
                {
                    Advance();
                    SkipRestOfLine();
                    break;
                }
                if (_openEnvironments.Contains(current.Value))
                {
                    Warn(TexinfoWarningCategory.Syntax, token.Position,
                        $"'@{name}' is missing its '@end {name}'.");
                    break;
                }
                Advance();
                SkipRestOfLine();
                continue;
            }
            if (current.Kind == TexinfoTokenKind.Command && current.AtLineStart
                && current.Value == "detailmenu")
            {
                entries.AddRange(ParseMenu(current, isDetailed: true).Entries);
                continue;
            }
            SourcePosition linePosition = current.Position;
            string line = ReadRawLine();
            if (line.StartsWith("*", StringComparison.Ordinal))
            {
                entries.Add(ParseMenuEntryLine(line, linePosition));
            }
        }
        return new MenuNode(isDetailed, entries, token.Position);
    }

    private static MenuEntryNode ParseMenuEntryLine(string line, SourcePosition position)
    {
        string rest = line.Substring(1).TrimStart();
        int doubleColon = rest.IndexOf("::", StringComparison.Ordinal);
        if (doubleColon >= 0)
        {
            return new MenuEntryNode(rest.Substring(0, doubleColon).Trim(), string.Empty,
                rest.Substring(doubleColon + 2).Trim(), position);
        }
        int colon = rest.IndexOf(':');
        if (colon >= 0)
        {
            string label = rest.Substring(0, colon).Trim();
            string tail = rest.Substring(colon + 1).TrimStart();
            int period = tail.IndexOf('.');
            string nodeName = period >= 0 ? tail.Substring(0, period).Trim() : tail.Trim();
            string description = period >= 0 ? tail.Substring(period + 1).Trim() : string.Empty;
            return new MenuEntryNode(nodeName, label, description, position);
        }
        return new MenuEntryNode(rest.Trim(), string.Empty, string.Empty, position);
    }

    // ----- Index entries -----------------------------------------------------------------------

    private IndexEntryNode BuildIndexEntry(TexinfoToken token, string indexName, ParseScope scope)
    {
        List<TexinfoNode> content = ParseRestOfLine(scope);
        string sortKey = string.Empty;
        List<TexinfoNode> filtered = new List<TexinfoNode>();
        foreach (TexinfoNode node in content)
        {
            if (node is InlineCommandNode command && command.Style == InlineStyle.SortAs)
            {
                sortKey = InlineNodes.ToPlainText(command.Content);
                continue;
            }
            filtered.Add(node);
        }
        IndexEntryNode entry = new IndexEntryNode(indexName, token.Value, InlineNodes.Trim(filtered),
            sortKey, token.Position)
        {
            Section = _currentSection
        };
        _document.IndexEntries.Add(entry);
        return entry;
    }

    private void ParseIndexMerge(TexinfoToken token)
    {
        Advance();
        string[] parts = ReadRawLine().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{token.Value}' needs two index names; it was ignored.");
            return;
        }
        _document.IndexMerges.Add(new IndexMerge(parts[0], parts[1], token.Value == "syncodeindex"));
    }

    // ----- Inline content ----------------------------------------------------------------------

    private List<TexinfoNode> ParseRestOfLine(ParseScope scope)
    {
        List<TexinfoNode> content = new List<TexinfoNode>();
        while (true)
        {
            TexinfoToken token = Peek();
            if (token.Kind == TexinfoTokenKind.EndOfInput || token.Kind == TexinfoTokenKind.EndCommand)
            {
                break;
            }
            if (IsScopeTerminator(token, scope))
            {
                break;
            }
            if (token.Kind == TexinfoTokenKind.Newline)
            {
                Advance();
                break;
            }
            Advance();
            switch (token.Kind)
            {
                case TexinfoTokenKind.Text:
                    content.Add(new TextNode(token.Value, token.Position));
                    break;
                case TexinfoTokenKind.OpenBrace:
                    content.Add(new TextNode("{", token.Position));
                    break;
                case TexinfoTokenKind.CloseBrace:
                    content.Add(new TextNode("}", token.Position));
                    break;
                case TexinfoTokenKind.RawBlock:
                    content.Add(BuildRawBlockNode(token, inline: true));
                    break;
                default:
                    ParseInlineCommandToken(token, content, scope);
                    break;
            }
        }
        return InlineNodes.Trim(content);
    }

    private List<TexinfoNode> ParseBraceGroup(ParseScope scope)
    {
        List<TexinfoNode> content = new List<TexinfoNode>();
        int depth = 1;
        while (true)
        {
            TexinfoToken token = Peek();
            if (token.Kind == TexinfoTokenKind.EndOfInput)
            {
                Warn(TexinfoWarningCategory.Syntax, token.Position, "A brace group is never closed.");
                break;
            }
            if (token.Kind == TexinfoTokenKind.EndCommand)
            {
                Warn(TexinfoWarningCategory.Syntax, token.Position,
                    $"'@end {token.Value}' appears inside a brace group that is never closed.");
                break;
            }
            Advance();
            switch (token.Kind)
            {
                case TexinfoTokenKind.OpenBrace:
                    depth++;
                    content.Add(new TextNode("{", token.Position));
                    break;
                case TexinfoTokenKind.CloseBrace:
                    depth--;
                    if (depth == 0)
                    {
                        return content;
                    }
                    content.Add(new TextNode("}", token.Position));
                    break;
                case TexinfoTokenKind.Newline:
                    content.Add(new TextNode("\n", token.Position));
                    break;
                case TexinfoTokenKind.Text:
                    content.Add(new TextNode(token.Value, token.Position));
                    break;
                case TexinfoTokenKind.RawBlock:
                    content.Add(BuildRawBlockNode(token, inline: true));
                    break;
                default:
                    ParseInlineCommandToken(token, content, scope);
                    break;
            }
        }
        return content;
    }

    private List<TexinfoNode> ParseOptionalBraceGroup(ParseScope scope)
    {
        if (Peek().Kind != TexinfoTokenKind.OpenBrace)
        {
            return new List<TexinfoNode>();
        }
        Advance();
        return ParseBraceGroup(scope);
    }

    /// <summary>
    /// Handles one command in inline position. The command token has already been consumed by the
    /// caller; anything the command needs after it - a brace group, the rest of the line - is read
    /// here.
    /// </summary>
    private void ParseInlineCommandToken(TexinfoToken token, List<TexinfoNode> into, ParseScope scope)
    {
        string name = token.Value;

        if (name.Length == 1 && !char.IsLetter(name[0]))
        {
            ParseSingleCharacterCommand(token, into, scope);
            return;
        }

        if (TexinfoCommandTable.TryGetAccent(name, out string accentMark))
        {
            into.Add(BuildAccent(token, accentMark, scope));
            return;
        }

        switch (name)
        {
            case "dotless":
                into.Add(BuildDotless(token, scope));
                return;
            case "exdent":
                //Reachable only inside a preformatted environment, where every line already sits
                //where it was written and there is no indentation to stand clear of. Dropping the
                //command keeps the line it introduced, which is the whole of its content.
                return;
            case "acronym":
            case "abbr":
                into.Add(BuildAcronym(token, scope));
                return;
            case "ref":
            case "xref":
            case "pxref":
            case "inforef":
                into.Add(BuildCrossReference(token, scope));
                return;
            case "url":
            case "uref":
                into.Add(BuildLink(token, LinkKind.Url, scope));
                return;
            case "email":
                into.Add(BuildLink(token, LinkKind.Email, scope));
                return;
            case "image":
                into.Add(BuildImage(token, scope));
                return;
            case "footnote":
                into.Add(BuildFootnote(token, scope));
                return;
            case "anchor":
                into.Add(BuildAnchor(token, scope));
                return;
            case "U":
                into.Add(BuildUnicodeGlyph(token, scope));
                return;
            case "today":
                DiscardGlyphBraces(token, scope);
                into.Add(new GlyphNode(name,
                    DateTime.Now.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), token.Position));
                return;
        }

        if (TexinfoCommandTable.TryGetGlyph(name, out string glyph))
        {
            DiscardGlyphBraces(token, scope);
            into.Add(new GlyphNode(name, glyph, token.Position));
            return;
        }

        if (TexinfoCommandTable.TryGetInlineStyle(name, out InlineStyle style))
        {
            into.Add(new InlineCommandNode(name, style, ParseOptionalBraceGroup(scope), token.Position));
            return;
        }

        if (TexinfoCommandTable.IsDiscardedBraceCommand(name))
        {
            ParseOptionalBraceGroup(scope);
            return;
        }

        if (TexinfoCommandTable.TryGetIndexName(name, out string indexName)
            || _userIndexCommands.TryGetValue(name, out indexName))
        {
            into.Add(BuildIndexEntry(token, indexName, scope));
            return;
        }

        Warn(TexinfoWarningCategory.UnknownCommand, token.Position,
            $"'@{name}' is not supported; its argument text was kept.");
        into.Add(new UnknownCommandNode(name, ParseOptionalBraceGroup(scope), isBlock: false,
            token.Position));
    }

    private void ParseSingleCharacterCommand(TexinfoToken token, List<TexinfoNode> into,
        ParseScope scope)
    {
        if (TexinfoCommandTable.TryGetAccent(token.Value, out string mark))
        {
            into.Add(BuildAccent(token, mark, scope));
            return;
        }
        switch (token.Value)
        {
            case "@":
            case "{":
            case "}":
            case "&":
            case ".":
            case "!":
            case "?":
                into.Add(new TextNode(token.Value, token.Position));
                return;
            case "*":
                into.Add(new LineBreakNode(token.Position));
                return;
            case " ":
            case "\t":
            case "\n":
                into.Add(new TextNode(" ", token.Position));
                return;
            case "-":
                into.Add(new TextNode("­", token.Position));
                return;
            case ":":
            case "/":
            case "|":
                // Typesetting hints with no counterpart in the output subset.
                return;
            default:
                Warn(TexinfoWarningCategory.UnknownCommand, token.Position,
                    $"'@{token.Value}' is not supported; it was dropped.");
                return;
        }
    }

    // ----- Accents -----------------------------------------------------------------------------

    private GlyphNode BuildAccent(TexinfoToken token, string mark, ParseScope scope)
        => new GlyphNode(token.Value,
            ComposeAccent(ReadAccentArgument(token.Value, scope), mark), token.Position);

    private GlyphNode BuildDotless(TexinfoToken token, ParseScope scope)
    {
        string letter = ReadAccentArgument(token.Value, scope).Trim();
        if (TexinfoCommandTable.TryGetDotless(letter, out string dotless))
        {
            return new GlyphNode(token.Value, dotless, token.Position);
        }
        Warn(TexinfoWarningCategory.Syntax, token.Position,
            $"'@dotless{{{letter}}}' names no letter that has a dot to remove; it was kept as written.");
        return new GlyphNode(token.Value, letter, token.Position);
    }

    /// <summary>
    /// Reads what an accent command applies to. Texinfo lets the punctuation accents be written
    /// with or without braces - <c>@'e</c> and <c>@'{e}</c> are the same thing - so without braces
    /// the single character that follows is taken. The alphabetic commands need something between
    /// the name and the letter, so for those a space is skipped rather than accented.
    /// </summary>
    private string ReadAccentArgument(string name, ParseScope scope)
    {
        if (Peek().Kind == TexinfoTokenKind.OpenBrace)
        {
            Advance();
            return InlineNodes.ToPlainText(ParseBraceGroup(scope));
        }
        return TakeOneCharacter(TexinfoCommandTable.IsAlphabeticAccent(name));
    }

    private string TakeOneCharacter(bool skipLeadingSpaces)
    {
        TexinfoToken token = Peek();
        if (token.Kind != TexinfoTokenKind.Text || token.Value.Length == 0)
        {
            return string.Empty;
        }
        string text = token.Value;
        int start = 0;
        if (skipLeadingSpaces)
        {
            while (start < text.Length && (text[start] == ' ' || text[start] == '\t'))
            {
                start++;
            }
            if (start == text.Length)
            {
                return string.Empty;
            }
        }
        Advance();
        int length = char.IsHighSurrogate(text[start]) && start + 1 < text.Length ? 2 : 1;
        if (start + length < text.Length)
        {
            PushBackText(text.Substring(start + length), token.Position);
        }
        return text.Substring(start, length);
    }

    /// <summary>
    /// Puts a combining mark on the character it applies to and composes the result. A combining
    /// mark follows its base character; for the tie, which spans two, that same rule places it
    /// between them. Normalizing afterwards yields the precomposed character wherever Unicode has
    /// one, which is what a text font is far more likely to carry a glyph for.
    /// </summary>
    private static string ComposeAccent(string baseText, string mark)
    {
        if (baseText.Length == 0)
        {
            return mark;
        }
        int first = char.IsHighSurrogate(baseText[0]) && baseText.Length > 1 ? 2 : 1;
        string combined = baseText.Substring(0, first) + mark + baseText.Substring(first);
        try
        {
            return combined.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            //Text that is not well-formed Unicode cannot be normalized; the composed form still
            //reads, and losing the accent would be worse than losing the composition.
            return combined;
        }
    }

    private AcronymNode BuildAcronym(TexinfoToken token, ParseScope scope)
    {
        List<List<TexinfoNode>> parts = InlineNodes.SplitOnCommas(ParseOptionalBraceGroup(scope), 2);
        return new AcronymNode(token.Value == "acronym", InlineNodes.Part(parts, 0),
            InlineNodes.Part(parts, 1), token.Position);
    }

    private void DiscardGlyphBraces(TexinfoToken token, ParseScope scope)
    {
        if (Peek().Kind != TexinfoTokenKind.OpenBrace)
        {
            return;
        }
        Advance();
        List<TexinfoNode> content = ParseBraceGroup(scope);
        if (InlineNodes.HasVisibleContent(content))
        {
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{token.Value}' takes no argument; the text inside its braces was dropped.");
        }
    }

    private CrossReferenceNode BuildCrossReference(TexinfoToken token, ParseScope scope)
    {
        CrossReferenceKind kind;
        switch (token.Value)
        {
            case "xref":
                kind = CrossReferenceKind.SentenceStart;
                break;
            case "pxref":
                kind = CrossReferenceKind.Parenthetical;
                break;
            case "inforef":
                kind = CrossReferenceKind.InfoReference;
                break;
            default:
                kind = CrossReferenceKind.Reference;
                break;
        }
        List<List<TexinfoNode>> parts = InlineNodes.SplitOnCommas(ParseOptionalBraceGroup(scope), 5);
        return new CrossReferenceNode(kind, InlineNodes.PartName(parts, 0), InlineNodes.PartText(parts, 1),
            InlineNodes.Part(parts, 2), InlineNodes.PartText(parts, 3), InlineNodes.PartText(parts, 4),
            token.Position);
    }

    private LinkNode BuildLink(TexinfoToken token, LinkKind kind, ParseScope scope)
    {
        List<List<TexinfoNode>> parts = InlineNodes.SplitOnCommas(ParseOptionalBraceGroup(scope), 3);
        return new LinkNode(kind, InlineNodes.PartText(parts, 0), InlineNodes.Part(parts, 1),
            InlineNodes.PartText(parts, 2), token.Position);
    }

    private ImageNode BuildImage(TexinfoToken token, ParseScope scope)
    {
        List<List<TexinfoNode>> parts = InlineNodes.SplitOnCommas(ParseOptionalBraceGroup(scope), 5);
        return new ImageNode(InlineNodes.PartText(parts, 0), InlineNodes.PartText(parts, 1),
            InlineNodes.PartText(parts, 2), InlineNodes.PartText(parts, 3),
            InlineNodes.PartText(parts, 4), token.Position);
    }

    private FootnoteNode BuildFootnote(TexinfoToken token, ParseScope scope)
    {
        FootnoteNode footnote = new FootnoteNode(_document.Footnotes.Count + 1,
            ParseOptionalBraceGroup(scope), token.Position);
        _document.Footnotes.Add(footnote);
        return footnote;
    }

    private AnchorNode BuildAnchor(TexinfoToken token, ParseScope scope)
    {
        string name = InlineNodes.ToName(ParseOptionalBraceGroup(scope));
        AnchorNode anchor = new AnchorNode(name, token.Position);
        RegisterAnchor(name, TexinfoAnchorKind.Anchor, anchor, token.Position);
        return anchor;
    }

    private TexinfoNode BuildUnicodeGlyph(TexinfoToken token, ParseScope scope)
    {
        string text = InlineNodes.ToPlainText(ParseOptionalBraceGroup(scope)).Trim();
        if (int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint)
            && codePoint >= 0 && codePoint <= 0x10FFFF
            && !(codePoint >= 0xD800 && codePoint <= 0xDFFF))
        {
            return new GlyphNode("U", char.ConvertFromUtf32(codePoint), token.Position);
        }
        Warn(TexinfoWarningCategory.Syntax, token.Position,
            $"'@U{{{text}}}' is not a Unicode code point; it was dropped.");
        return new GlyphNode("U", string.Empty, token.Position);
    }
}
