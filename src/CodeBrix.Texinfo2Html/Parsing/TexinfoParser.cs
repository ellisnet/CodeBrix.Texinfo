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
    private int _index;
    private bool _sawBye;
    private SectionNode _currentSection;
    private NodeAnchorNode _pendingNode;
    private TexinfoAnchor _pendingAnchor;

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
        => _index < _tokens.Count
            ? _tokens[_index]
            : _tokens[_tokens.Count - 1];

    private void Advance()
    {
        if (_index < _tokens.Count)
        {
            _index++;
        }
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
            int before = _index;
            ParseBlockToken(token, builder, scope);
            if (_index == before)
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
            ParseSingleCharacterCommand(token, builder.Paragraph(token.Position));
            return;
        }

        if (TexinfoCommandTable.TryGetBlockEnvironment(name, out TexinfoBlockKind blockKind,
                out bool isPreformatted))
        {
            ParseEnvironment(token, blockKind, isPreformatted, builder);
            return;
        }

        if (TexinfoCommandTable.IsGenericBlockEnvironment(name))
        {
            ParseGenericEnvironment(token, builder);
            return;
        }

        switch (name)
        {
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
            builder.AddBlock(new HeadingNode(name, headingKind, headingLevel, ParseRestOfLine(scope),
                token.Position));
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
            TexinfoCommandTable.TryGetSectioning(name, out int level, out _);
            Warn(TexinfoWarningCategory.Syntax, token.Position,
                $"'@{name}' cannot open a section inside an environment; it was rendered as a heading.");
            Advance();
            builder.AddBlock(new HeadingNode(name, LevelToHeadingKind(level), level, ParseRestOfLine(scope),
                token.Position));
            return;
        }

        Advance();
        ParseInlineCommandToken(token, builder.Paragraph(token.Position), scope);
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
        string nodeName = InlineNodes.PartText(parts, 0);
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
            if (token.Kind == TexinfoTokenKind.Command && token.AtLineStart && token.Value == "group")
            {
                // @group inside a preformatted block only asks for page-break protection.
                Advance();
                SkipRestOfLine();
                continue;
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
            int before = _index;
            ParseBlockSequence(itemScope, leading);
            if (_index == before)
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
                terms.Add(new TableTermNode(term, isContinuation, current.Position));
                ParseBlockSequence(itemScope, blocks);
                continue;
            }
            if (current.Kind == TexinfoTokenKind.Newline
                || (current.Kind == TexinfoTokenKind.Text && IsWhitespaceOnly(current.Value)))
            {
                Advance();
                continue;
            }
            int before = _index;
            ParseBlockSequence(itemScope, blocks);
            if (_index == before)
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
            int before = _index;
            List<TexinfoNode> stray = new List<TexinfoNode>();
            ParseBlockSequence(cellScope, stray);
            if (_index == before)
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
            ParseSingleCharacterCommand(token, into);
            return;
        }

        switch (name)
        {
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

        if (TexinfoCommandTable.TryGetIndexName(name, out string indexName))
        {
            into.Add(BuildIndexEntry(token, indexName, scope));
            return;
        }

        Warn(TexinfoWarningCategory.UnknownCommand, token.Position,
            $"'@{name}' is not supported; its argument text was kept.");
        into.Add(new UnknownCommandNode(name, ParseOptionalBraceGroup(scope), isBlock: false,
            token.Position));
    }

    private void ParseSingleCharacterCommand(TexinfoToken token, List<TexinfoNode> into)
    {
        switch (token.Value)
        {
            case "@":
            case "{":
            case "}":
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
        return new CrossReferenceNode(kind, InlineNodes.PartText(parts, 0), InlineNodes.PartText(parts, 1),
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
        string name = InlineNodes.ToPlainText(ParseOptionalBraceGroup(scope)).Trim();
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
