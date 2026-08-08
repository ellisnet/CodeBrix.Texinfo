using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Parsing;
using CodeBrix.Texinfo2Html.Semantics;
using CodeBrix.Texinfo2Html.Snippets;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Emit;

/// <summary>
/// Walks an analyzed document and writes the body markup. Every element and every CSS class it
/// produces is one that CodeBrix.PdfDocCreate.Html2Pdf implements, because this markup exists to be
/// turned into a PDF rather than to be shown in a browser.
/// </summary>
/// <remarks>
/// Nothing here throws. A construct the emitter cannot render becomes a warning plus the closest
/// readable degradation, on the same principle the rest of the library follows: one unrenderable
/// paragraph must not cost the reader the other ten thousand.
/// </remarks>
internal sealed class HtmlEmitter
{
    /// <summary>
    /// One step of indentation for a preformatted environment written inside another one. Five
    /// spaces is the step Texinfo's own printed output uses between such levels, and inside a
    /// preformatted element spaces are the only unit that means anything.
    /// </summary>
    private const string NestedPreformattedIndent = "     ";

    private readonly TexinfoDocument _document;
    private readonly DocumentSemantics _semantics;
    private readonly ImageReferenceResolver _images;
    private readonly SnippetRenderCoordinator _snippets;
    private readonly TexinfoWarningCollection _warnings;
    private readonly HtmlWriter _writer = new HtmlWriter();
    private bool _warnedAboutMath;
    private bool _titlePageEmitted;
    private int _literalDepth;
    private int _noBreakDepth;
    private int _repeatedDepth;
    private int _nestedPreformattedDepth;
    private bool _nestedIndentPending;
    private int _unresolvedReferenceCount;
    private string _firstUnresolvedReference = string.Empty;
    private SourcePosition _firstUnresolvedPosition;

    /// <summary>Creates an emitter for one analyzed document.</summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="semantics">The results of the semantic passes over that document.</param>
    /// <param name="images">Resolver used to turn image references into paths on disk.</param>
    /// <param name="snippets">Coordinator that engraves the document's music environments.</param>
    public HtmlEmitter(TexinfoDocument document, DocumentSemantics semantics,
        ImageReferenceResolver images, SnippetRenderCoordinator snippets)
    {
        _document = document;
        _semantics = semantics;
        _images = images;
        _snippets = snippets;
        _warnings = document.Warnings;
    }

    /// <summary>Writes the document body and returns the markup.</summary>
    public string EmitBody()
    {
        //A printed manual opens with its title page. Texinfo lets @titlepage sit anywhere in the
        //preamble, and a document read under the print profile can end up with it inside the
        //@top node, so it is emitted here and skipped where it was written.
        if (_semantics.TitlePage != null)
        {
            EmitEnvironment(_semantics.TitlePage);
            _titlePageEmitted = true;
        }
        EmitBlocks(_document.Preamble);
        foreach (SectionNode section in _document.Sections)
        {
            EmitSection(section);
        }
        EmitFootnoteList(_semantics.TrailingFootnotes);
        _snippets.ReportTotals();
        ReportUnresolvedReferences();
        return _writer.ToString();
    }

    // ----- sectioning ------------------------------------------------------------------------

    private void EmitSection(SectionNode section)
    {
        string tag = "h" + (section.HeadingLevel < 1 ? 1 : section.HeadingLevel)
            .ToString(CultureInfo.InvariantCulture);
        _writer.BeginBlock(tag);
        _writer.Attribute("id", section.ElementId);
        _writer.Attribute("class", StartsOnANewPage(section) ? "texinfo-chapter" : null);
        _writer.CloseStartTag();
        if (section.Number.Length > 0)
        {
            _writer.BeginInline("span");
            _writer.Attribute("class", "texinfo-secnum");
            _writer.CloseStartTag();
            _writer.Text(section.Number);
            _writer.EndInline("span");
            _writer.Text(" ");
        }
        EmitInlines(section.Title);
        _writer.EndBlock(tag);

        EmitBlocks(section.Blocks);
        foreach (SectionNode child in section.Children)
        {
            EmitSection(child);
        }
        //A printed manual prints its notes at the end of the chapter they belong to, which is only
        //knowable once the chapter's last subsection has been written out.
        EmitFootnoteList(_semantics.FootnotesFor(section));
    }

    /// <summary>
    /// True when a sectioning unit begins a fresh page. In a printed manual a chapter does, which
    /// is Texinfo's own default; <c>@setchapternewpage off</c> is how a document asks for the
    /// running text a screen reader gets instead. <c>@top</c> is excluded because it names the
    /// document rather than opening a chapter of it.
    /// </summary>
    private bool StartsOnANewPage(SectionNode section)
    {
        if (section.Kind == SectionKind.Top || section.Level > 1)
        {
            return false;
        }
        return !(_document.Settings.TryGetValue("setchapternewpage", out string setting)
                 && setting.Trim().Equals("off", StringComparison.OrdinalIgnoreCase));
    }

    // ----- blocks ----------------------------------------------------------------------------

    private void EmitBlocks(IReadOnlyList<TexinfoNode> blocks)
    {
        foreach (TexinfoNode node in blocks)
        {
            EmitBlock(node);
        }
    }

    private void EmitBlock(TexinfoNode node)
    {
        switch (node)
        {
            case ParagraphNode paragraph:
                EmitParagraph(paragraph);
                return;
            case SectionNode section:
                EmitSection(section);
                return;
            case HeadingNode heading:
                EmitHeading(heading);
                return;
            case PreformattedNode preformatted:
                EmitPreformatted(preformatted);
                return;
            case VerbatimNode verbatim:
                EmitVerbatim(verbatim);
                return;
            case BlockEnvironmentNode environment:
                EmitEnvironment(environment);
                return;
            case ListNode list:
                EmitList(list);
                return;
            case TableNode table:
                EmitDefinitionList(table);
                return;
            case MultitableNode multitable:
                EmitMultitable(multitable);
                return;
            case DefinitionNode definition:
                EmitDefinition(definition);
                return;
            case FloatNode floatNode:
                EmitFloat(floatNode);
                return;
            case DirectiveNode directive:
                EmitDirective(directive);
                return;
            case AnchorNode anchor:
                EmitAnchorBlock(_semantics.ElementIdFor(anchor.Name));
                return;
            case NodeAnchorNode nodeAnchor:
                EmitAnchorBlock(_semantics.ElementIdFor(nodeAnchor.NodeName));
                return;
            case ImageNode image:
                EmitImage(image, asBlock: true);
                return;
            case MusicSnippetNode snippet:
                EmitMusicSnippet(snippet, asBlock: true);
                return;
            case UnknownCommandNode unknown:
                EmitContainer("div", "texinfo-unknown", unknown.Content);
                return;
            case IndexEntryNode entry:
                //An index entry is not content: it marks the place the printed index links back to.
                EmitAnchorBlock(_semantics.IndexEntryIdFor(entry));
                return;
            case MenuNode:
                //Menus are navigation for the Info reader, not content in a printed document.
                return;
            default:
                //Anything else the parser can produce is inline content that ended up in a block
                //slot; a paragraph is the honest container for it.
                EmitParagraphOf(new[] { node });
                return;
        }
    }

    private void EmitParagraph(ParagraphNode paragraph)
    {
        string cssClass;
        switch (paragraph.Alignment)
        {
            case ParagraphAlignment.Centered:
                cssClass = "texinfo-center";
                break;
            case ParagraphAlignment.Exdented:
                cssClass = "texinfo-exdent";
                break;
            default:
                cssClass = paragraph.SuppressIndent ? "texinfo-noindent" : string.Empty;
                break;
        }
        _writer.BeginBlock("p");
        _writer.Attribute("class", cssClass);
        _writer.CloseStartTag();
        EmitInlines(paragraph.Content);
        _writer.EndBlock("p");
    }

    private void EmitParagraphOf(IReadOnlyList<TexinfoNode> content)
    {
        _writer.BeginBlock("p");
        _writer.CloseStartTag();
        EmitInlines(content);
        _writer.EndBlock("p");
    }

    private void EmitHeading(HeadingNode heading)
    {
        string cssClass;
        switch (heading.Kind)
        {
            case HeadingKind.Title:
                cssClass = "texinfo-title";
                break;
            case HeadingKind.Subtitle:
                cssClass = "texinfo-subtitle";
                break;
            case HeadingKind.Author:
                cssClass = "texinfo-author";
                break;
            default:
                int level = heading.Level < 1 ? 1 : heading.Level > 5 ? 5 : heading.Level;
                cssClass = "texinfo-heading-" + level.ToString(CultureInfo.InvariantCulture);
                break;
        }
        //The @heading family is deliberately not emitted as h1-h6: those commands print a heading
        //without creating structure, and an h-element would put them in the PDF outline.
        _writer.BeginBlock("p");
        _writer.Attribute("class", cssClass);
        _writer.CloseStartTag();
        EmitInlines(heading.Content);
        _writer.EndBlock("p");
    }

    private void EmitPreformatted(PreformattedNode preformatted)
    {
        if (preformatted.Kind == TexinfoBlockKind.DisplayMath)
        {
            WarnAboutMathematics(preformatted.Position);
        }
        bool literal = IsLiteralBlock(preformatted.Kind);
        _writer.BeginBlock("pre");
        _writer.Attribute("class", PreformattedClass(preformatted.Kind));
        _writer.CloseStartTag();
        _writer.BeginPreformatted();
        if (literal)
        {
            _literalDepth++;
        }
        EmitInlines(preformatted.Content);
        if (literal)
        {
            _literalDepth--;
        }
        _writer.EndBlock("pre");
        _writer.EndPreformatted();
    }

    /// <summary>
    /// Emits a preformatted environment that sits inside another one. There is no second
    /// <c>&lt;pre&gt;</c> to open - the output subset has none and nothing here needs one, because
    /// the text is already whitespace-preserved. One more step of indentation is exactly what the
    /// nesting means, and it is what Texinfo's own printed output makes of it. The inner block
    /// keeps its own literalness, so an <c>@example</c> inside a <c>@display</c> still has its
    /// characters left alone while the prose around it is converted.
    /// </summary>
    private void EmitNestedPreformatted(PreformattedNode nested)
    {
        bool literal = IsLiteralBlock(nested.Kind);
        bool outerPending = _nestedIndentPending;
        _nestedPreformattedDepth++;
        //The indentation is written when a line turns out to HAVE content, never on the newline
        //that ended the one before. That is what keeps a blank line blank and stops the block's
        //last line break from leaving indentation trailing behind it.
        _nestedIndentPending = true;
        if (literal)
        {
            _literalDepth++;
        }
        EmitInlines(nested.Content);
        if (literal)
        {
            _literalDepth--;
        }
        _nestedPreformattedDepth--;
        _nestedIndentPending = outerPending;
    }

    /// <summary>
    /// Writes the pending indentation of a nested preformatted block before a node that is about
    /// to open an element, so that the indentation sits outside the element rather than inside it.
    /// Text nodes are excluded: they indent themselves, character by character, in
    /// <see cref="WriteText"/>.
    /// </summary>
    private void FlushNestedIndentBefore(TexinfoNode node)
    {
        if (_nestedPreformattedDepth > 0 && _nestedIndentPending && !(node is TextNode))
        {
            _writer.Text(NestedIndentFor(_nestedPreformattedDepth));
            _nestedIndentPending = false;
        }
    }

    private static bool IsLiteralBlock(TexinfoBlockKind kind)
    {
        //@example and @lisp hold code, so the text conventions leave their content alone. @display
        //and @format only preserve line breaks and indentation; their content is ordinary prose and
        //is converted like any other.
        switch (kind)
        {
            case TexinfoBlockKind.Example:
            case TexinfoBlockKind.SmallExample:
            case TexinfoBlockKind.Lisp:
            case TexinfoBlockKind.SmallLisp:
            //A displayed equation is written in TeX's notation, so its characters are as literal
            //as any other computer text: nothing in it is prose to convert.
            case TexinfoBlockKind.DisplayMath:
                return true;
            default:
                return false;
        }
    }

    private static string PreformattedClass(TexinfoBlockKind kind)
    {
        switch (kind)
        {
            case TexinfoBlockKind.SmallExample:
            case TexinfoBlockKind.SmallLisp:
                return "texinfo-smallexample";
            case TexinfoBlockKind.Display:
            case TexinfoBlockKind.SmallDisplay:
                return "texinfo-display";
            case TexinfoBlockKind.Format:
            case TexinfoBlockKind.SmallFormat:
                return "texinfo-format";
            case TexinfoBlockKind.DisplayMath:
                return "texinfo-displaymath";
            default:
                return "texinfo-example";
        }
    }

    private void EmitVerbatim(VerbatimNode verbatim)
    {
        _writer.BeginBlock("pre");
        _writer.Attribute("class", "texinfo-verbatim");
        _writer.CloseStartTag();
        _writer.BeginPreformatted();
        _writer.Text(verbatim.Text);
        _writer.EndBlock("pre");
        _writer.EndPreformatted();
    }

    private void EmitEnvironment(BlockEnvironmentNode environment)
    {
        switch (environment.Kind)
        {
            case TexinfoBlockKind.Quotation:
            case TexinfoBlockKind.SmallQuotation:
                _writer.BeginBlock("blockquote");
                _writer.Attribute("class", "texinfo-quotation");
                _writer.CloseStartTag();
                if (InlineNodes.HasVisibleContent(environment.Argument))
                {
                    //@quotation takes an optional label - "Note", "Caution" - printed above it.
                    _writer.BeginBlock("p");
                    _writer.Attribute("class", "texinfo-quotation-label");
                    _writer.CloseStartTag();
                    EmitInlines(environment.Argument);
                    _writer.EndBlock("p");
                }
                EmitBlocks(environment.Blocks);
                _writer.EndBlock("blockquote");
                return;

            case TexinfoBlockKind.IndentedBlock:
            case TexinfoBlockKind.SmallIndentedBlock:
                EmitContainer("blockquote", "texinfo-indentedblock", environment.Blocks);
                return;

            case TexinfoBlockKind.Cartouche:
                EmitContainer("div", "texinfo-cartouche", environment.Blocks);
                return;

            case TexinfoBlockKind.RaggedRight:
                EmitContainer("div", "texinfo-raggedright", environment.Blocks);
                return;

            case TexinfoBlockKind.FlushLeft:
                EmitContainer("div", "texinfo-flushleft", environment.Blocks);
                return;

            case TexinfoBlockKind.FlushRight:
                EmitContainer("div", "texinfo-flushright", environment.Blocks);
                return;

            case TexinfoBlockKind.TitlePage:
                if (_titlePageEmitted && ReferenceEquals(environment, _semantics.TitlePage))
                {
                    //Already emitted at the front of the document.
                    return;
                }
                EmitContainer("div", "texinfo-titlepage", environment.Blocks);
                return;

            case TexinfoBlockKind.Group:
                //@group only asks that its content not be split across a page; it adds no structure.
                EmitBlocks(environment.Blocks);
                return;

            case TexinfoBlockKind.DefinitionBlock:
                EmitContainer("div", "texinfo-defblock", environment.Blocks);
                return;

            case TexinfoBlockKind.DocumentDescription:
                //Metadata for an HTML head, not content.
                return;

            default:
                EmitContainer("div", "texinfo-unknown", environment.Blocks);
                return;
        }
    }

    private void EmitContainer(string tagName, string cssClass, IReadOnlyList<TexinfoNode> blocks)
    {
        _writer.BeginBlock(tagName);
        _writer.Attribute("class", cssClass);
        _writer.CloseStartTag();
        EmitBlocks(blocks);
        _writer.EndBlock(tagName);
    }

    // ----- lists and tables ------------------------------------------------------------------

    private void EmitList(ListNode list)
    {
        string tag = list.IsEnumerated ? "ol" : "ul";
        _writer.BeginBlock(tag);
        if (list.IsEnumerated)
        {
            if (IsAllDigits(list.Marker))
            {
                _writer.Attribute("start", list.Marker);
            }
            else
            {
                string listStyle = EnumerateStyle(list.Marker);
                _writer.Attribute("style", listStyle.Length > 0 ? "list-style-type: " + listStyle : null);
            }
        }
        _writer.CloseStartTag();
        foreach (ListItemNode item in list.Items)
        {
            _writer.BeginBlock("li");
            _writer.CloseStartTag();
            EmitBlocks(item.Blocks);
            _writer.EndBlock("li");
        }
        _writer.EndBlock(tag);
    }

    private static bool IsAllDigits(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        foreach (char c in text)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    private static string EnumerateStyle(string marker)
    {
        switch (marker)
        {
            case "a":
                return "lower-alpha";
            case "A":
                return "upper-alpha";
            case "i":
                return "lower-roman";
            case "I":
                return "upper-roman";
            default:
                return string.Empty;
        }
    }

    private void EmitDefinitionList(TableNode table)
    {
        _writer.BeginBlock("dl");
        _writer.Attribute("class", "texinfo-table");
        _writer.CloseStartTag();
        bool hasStyle = TexinfoCommandTable.TryGetInlineStyle(table.FormatCommand, out InlineStyle style);
        foreach (TableEntryNode entry in table.Entries)
        {
            foreach (TableTermNode term in entry.Terms)
            {
                _writer.BeginBlock("dt");
                _writer.CloseStartTag();
                //An @ftable or @vtable term is also an index entry, and the printed index links
                //back to the marker left here.
                EmitInlineAnchor(_semantics.IndexEntryIdFor(term.IndexEntry));
                if (hasStyle && style != InlineStyle.AsIs)
                {
                    EmitStyled(style, term.Content);
                }
                else
                {
                    EmitInlines(term.Content);
                }
                _writer.EndBlock("dt");
            }
            if (entry.Blocks.Count > 0)
            {
                _writer.BeginBlock("dd");
                _writer.CloseStartTag();
                EmitBlocks(entry.Blocks);
                _writer.EndBlock("dd");
            }
        }
        _writer.EndBlock("dl");
    }

    private void EmitMultitable(MultitableNode multitable)
    {
        _writer.BeginBlock("table");
        _writer.Attribute("class", "texinfo-multitable");
        _writer.CloseStartTag();
        bool widthsWritten = false;
        foreach (MultitableRowNode row in multitable.Rows)
        {
            _writer.BeginBlock("tr");
            _writer.CloseStartTag();
            string cellTag = row.IsHeader ? "th" : "td";
            for (int i = 0; i < row.Cells.Count; i++)
            {
                _writer.BeginBlock(cellTag);
                //@columnfractions gives proportions for the whole table, so they are written once,
                //on the first row: Html2Pdf measures a column from whichever cell declares a width.
                if (!widthsWritten && i < multitable.ColumnFractions.Count)
                {
                    double percent = multitable.ColumnFractions[i] * 100.0;
                    if (percent > 0)
                    {
                        _writer.Attribute("style", "width: "
                            + percent.ToString("0.##", CultureInfo.InvariantCulture) + "%");
                    }
                }
                _writer.CloseStartTag();
                EmitBlocks(row.Cells[i].Blocks);
                _writer.EndBlock(cellTag);
            }
            _writer.EndBlock("tr");
            widthsWritten = true;
        }
        _writer.EndBlock("table");
    }

    // ----- definitions and floats --------------------------------------------------------------

    /// <summary>
    /// Writes a definition as a description list: one term per heading line, and one description
    /// under them all. Texinfo prints the category out at the right margin, which needs a floating
    /// box the output subset does not have; it is written at the head of the line instead, which is
    /// where the Info output puts it and where it still labels what follows.
    /// </summary>
    private void EmitDefinition(DefinitionNode definition)
    {
        _writer.BeginBlock("dl");
        _writer.Attribute("class", "texinfo-definition");
        _writer.CloseStartTag();
        foreach (DefinitionHeaderNode header in definition.Headers)
        {
            EmitDefinitionHeader(header);
        }
        if (definition.Blocks.Count > 0)
        {
            _writer.BeginBlock("dd");
            _writer.CloseStartTag();
            EmitBlocks(definition.Blocks);
            _writer.EndBlock("dd");
        }
        _writer.EndBlock("dl");
    }

    private void EmitDefinitionHeader(DefinitionHeaderNode header)
    {
        _writer.BeginBlock("dt");
        _writer.Attribute("class", "texinfo-def-line");
        _writer.CloseStartTag();
        EmitInlineAnchor(_semantics.IndexEntryIdFor(header.IndexEntry));

        if (header.Category.Count > 0)
        {
            _writer.BeginInline("span");
            _writer.Attribute("class", "texinfo-def-category");
            _writer.CloseStartTag();
            EmitInlines(header.Category);
            if (header.ClassName.Count > 0 && header.ClassPreposition.Length > 0)
            {
                _writer.Text(" " + header.ClassPreposition + " ");
                EmitInlines(header.ClassName);
            }
            _writer.Text(":");
            _writer.EndInline("span");
            _writer.Text(" ");
        }
        if (header.DataType.Count > 0)
        {
            EmitDefinitionCode("texinfo-def-type", header.DataType);
            _writer.Text(" ");
        }
        EmitDefinitionCode("texinfo-def-name", header.Name);
        if (header.Arguments.Count > 0)
        {
            if (!OmitsSpaceAfterName(header))
            {
                _writer.Text(" ");
            }
            //A typed definition sets its whole line as computer text; an untyped one sets its
            //arguments as the metasyntactic variables they stand for, which is what the Texinfo
            //manual asks for and what keeps a '--' in an option name out of the en-dash rule.
            if (header.IsTyped)
            {
                EmitDefinitionCode("texinfo-def-arg", header.Arguments);
            }
            else
            {
                _writer.BeginInline("i");
                _writer.Attribute("class", "texinfo-def-arg");
                _writer.CloseStartTag();
                EmitInlines(header.Arguments);
                _writer.EndInline("i");
            }
        }
        _writer.EndBlock("dt");
    }

    private void EmitDefinitionCode(string cssClass, IReadOnlyList<TexinfoNode> content)
    {
        _writer.BeginInline("code");
        _writer.Attribute("class", cssClass);
        _writer.CloseStartTag();
        _literalDepth++;
        EmitInlines(content);
        _literalDepth--;
        _writer.EndInline("code");
    }

    /// <summary>
    /// True when the <c>txidefnamenospace</c> flag asks for the space after the name to go, and
    /// the arguments open with the bracket that flag exists for.
    /// </summary>
    private bool OmitsSpaceAfterName(DefinitionHeaderNode header)
    {
        if (!_document.Values.ContainsKey("txidefnamenospace"))
        {
            return false;
        }
        string text = InlineNodes.ToPlainText(header.Arguments);
        return text.Length > 0 && (text[0] == '(' || text[0] == '[');
    }

    private void EmitFloat(FloatNode node)
    {
        _writer.BeginBlock("div");
        _writer.Attribute("class", "texinfo-float");
        _writer.Attribute("id", _semantics.ElementIdFor(node.Label));
        _writer.CloseStartTag();
        EmitBlocks(node.Blocks);
        if (node.ReferenceText.Length > 0 || node.Caption.Count > 0)
        {
            _writer.BeginBlock("p");
            _writer.Attribute("class", "texinfo-float-caption");
            _writer.CloseStartTag();
            if (node.ReferenceText.Length > 0)
            {
                _writer.Text(node.Caption.Count > 0
                    ? node.ReferenceText + ": "
                    : node.ReferenceText);
            }
            EmitInlines(node.Caption);
            _writer.EndBlock("p");
        }
        _writer.EndBlock("div");
    }

    private void EmitListOfFloats(DirectiveNode directive)
    {
        string typeName = directive.Argument.Trim();
        IReadOnlyList<FloatNode> floats = _semantics.FloatsOfType(typeName);
        if (floats.Count == 0)
        {
            _warnings.Add(TexinfoWarningCategory.Emit, directive.Position,
                $"'@listoffloats {typeName}' printed nothing: the document has no float of that type.");
            return;
        }
        _writer.BeginBlock("div");
        _writer.Attribute("class", "texinfo-listoffloats");
        _writer.CloseStartTag();
        foreach (FloatNode node in floats)
        {
            _writer.BeginBlock("p");
            _writer.Attribute("class", "texinfo-listoffloats-entry");
            _writer.CloseStartTag();
            string elementId = _semantics.ElementIdFor(node.Label);
            if (elementId.Length > 0)
            {
                _writer.BeginInline("a");
                _writer.Attribute("href", "#" + elementId);
                _writer.CloseStartTag();
            }
            _writer.Text(node.ReferenceText.Length > 0 ? node.ReferenceText : "(untitled)");
            if (elementId.Length > 0)
            {
                _writer.EndInline("a");
            }
            //A short caption exists precisely so that a list can carry something briefer than the
            //caption printed under the float itself.
            IReadOnlyList<TexinfoNode> description =
                node.ShortCaption.Count > 0 ? node.ShortCaption : node.Caption;
            if (description.Count > 0)
            {
                _writer.Text(": ");
                EmitRepeatedInlines(description);
            }
            _writer.EndBlock("p");
        }
        _writer.EndBlock("div");
    }

    // ----- directives ------------------------------------------------------------------------

    private void EmitDirective(DirectiveNode directive)
    {
        switch (directive.Kind)
        {
            case DirectiveKind.Contents:
                EmitTableOfContents(_semantics.Contents, "Table of Contents");
                return;
            case DirectiveKind.ShortContents:
                EmitTableOfContents(_semantics.ShortContents, "Short Contents");
                return;
            case DirectiveKind.InsertCopying:
                EmitBlocks(_document.Copying);
                return;
            case DirectiveKind.PageBreak:
                _writer.BeginBlock("div");
                _writer.Attribute("class", "texinfo-page-break");
                _writer.CloseStartTag();
                _writer.EndBlock("div");
                return;
            case DirectiveKind.VerticalSpace:
                EmitBlankLines(directive.Argument);
                return;
            case DirectiveKind.NeedSpace:
                //A hint that so much space should remain on the page; nothing to render.
                return;
            case DirectiveKind.PrintIndex:
                EmitIndex(directive);
                return;
            case DirectiveKind.ListOfFloats:
                EmitListOfFloats(directive);
                return;
            default:
                return;
        }
    }

    private void EmitTableOfContents(IReadOnlyList<TableOfContentsEntry> entries, string heading)
    {
        if (entries.Count == 0)
        {
            return;
        }
        _writer.BeginBlock("div");
        _writer.Attribute("class", "texinfo-contents");
        _writer.CloseStartTag();
        _writer.BeginBlock("p");
        _writer.Attribute("class", "texinfo-contents-heading");
        _writer.CloseStartTag();
        _writer.Text(heading);
        _writer.EndBlock("p");
        foreach (TableOfContentsEntry entry in entries)
        {
            int depth = entry.Depth > 4 ? 4 : entry.Depth;
            _writer.BeginBlock("p");
            _writer.Attribute("class", "texinfo-toc-" + depth.ToString(CultureInfo.InvariantCulture));
            _writer.CloseStartTag();
            _writer.BeginInline("a");
            _writer.Attribute("href", "#" + entry.Section.ElementId);
            _writer.CloseStartTag();
            if (entry.Section.Number.Length > 0)
            {
                _writer.Text(entry.Section.Number + " ");
            }
            EmitRepeatedInlines(entry.Section.Title);
            _writer.EndInline("a");
            _writer.EndBlock("p");
        }
        _writer.EndBlock("div");
    }

    // ----- indices ---------------------------------------------------------------------------

    private void EmitIndex(DirectiveNode directive)
    {
        string name = directive.Argument.Trim();
        PrintedIndex index = _semantics.IndexNamed(name);
        if (index == null || index.Entries.Count == 0)
        {
            _warnings.Add(TexinfoWarningCategory.Emit, directive.Position,
                $"'@printindex {name}' printed nothing: the document files no entries in that index.");
            return;
        }
        _writer.BeginBlock("div");
        _writer.Attribute("class", "texinfo-index");
        _writer.CloseStartTag();
        string letter = null;
        foreach (PrintedIndexEntry entry in index.Entries)
        {
            if (!string.Equals(letter, entry.Letter, StringComparison.Ordinal))
            {
                letter = entry.Letter;
                _writer.BeginBlock("p");
                _writer.Attribute("class", "texinfo-index-letter");
                _writer.CloseStartTag();
                _writer.Text(letter);
                _writer.EndBlock("p");
            }
            EmitIndexEntry(entry);
        }
        _writer.EndBlock("div");
    }

    private void EmitIndexEntry(PrintedIndexEntry entry)
    {
        _writer.BeginBlock("p");
        _writer.Attribute("class", "texinfo-index-entry");
        _writer.CloseStartTag();
        _writer.BeginInline("a");
        _writer.Attribute("href", entry.ElementId.Length > 0 ? "#" + entry.ElementId : null);
        _writer.CloseStartTag();
        if (entry.UseCodeFont)
        {
            _writer.BeginInline("code");
            _writer.CloseStartTag();
            _literalDepth++;
            EmitRepeatedInlines(entry.Source.Content);
            _literalDepth--;
            _writer.EndInline("code");
        }
        else
        {
            EmitRepeatedInlines(entry.Source.Content);
        }
        _writer.EndInline("a");
        //Without page numbers, which the markup cannot know, the section an entry was written in is
        //what tells two identically worded entries apart and what makes the line worth following.
        SectionNode section = entry.Source.Section;
        if (section == null || section.ElementId.Length == 0)
        {
            _writer.EndBlock("p");
            return;
        }
        _writer.BeginInline("span");
        _writer.Attribute("class", "texinfo-index-section");
        _writer.CloseStartTag();
        _writer.Text(" — ");
        _writer.BeginInline("a");
        _writer.Attribute("href", "#" + section.ElementId);
        _writer.CloseStartTag();
        EmitRepeatedInlines(section.Title);
        _writer.EndInline("a");
        _writer.EndInline("span");
        _writer.EndBlock("p");
    }

    private void EmitBlankLines(string argument)
    {
        //@sp takes a line count; @vskip takes a TeX dimension, which has no counterpart here, so a
        //single blank line stands in for it.
        int lines = 1;
        if (!string.IsNullOrEmpty(argument)
            && int.TryParse(argument.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int parsed))
        {
            lines = parsed;
        }
        if (lines > 20)
        {
            lines = 20;
        }
        for (int i = 0; i < lines; i++)
        {
            _writer.BeginBlock("p");
            _writer.Attribute("class", "texinfo-blank");
            _writer.CloseStartTag();
            _writer.Raw("&#160;");
            _writer.EndBlock("p");
        }
    }

    private void EmitAnchorBlock(string elementId)
    {
        if (elementId.Length == 0 || _repeatedDepth > 0)
        {
            return;
        }
        //A paragraph with no content at all is dropped before it can carry a bookmark, so the
        //marker holds a non-breaking space and is styled down to a single point.
        _writer.BeginBlock("p");
        _writer.Attribute("class", "texinfo-anchor");
        _writer.Attribute("id", elementId);
        _writer.CloseStartTag();
        _writer.Raw("&#160;");
        _writer.EndBlock("p");
    }

    // ----- footnotes -------------------------------------------------------------------------

    private void EmitFootnoteList(IReadOnlyList<FootnoteNode> footnotes)
    {
        if (footnotes.Count == 0)
        {
            return;
        }
        _writer.BeginBlock("div");
        _writer.Attribute("class", "texinfo-footnotes");
        _writer.CloseStartTag();
        _writer.BeginVoidBlock("hr");
        _writer.CloseStartTag();
        _writer.BeginBlock("p");
        _writer.Attribute("class", "texinfo-footnotes-heading");
        _writer.CloseStartTag();
        _writer.Text("Footnotes");
        _writer.EndBlock("p");
        foreach (FootnoteNode footnote in footnotes)
        {
            _writer.BeginBlock("p");
            _writer.Attribute("class", "texinfo-footnote-item");
            _writer.Attribute("id", _semantics.FootnoteIdFor(footnote));
            _writer.CloseStartTag();
            _writer.Text("(" + footnote.Number.ToString(CultureInfo.InvariantCulture) + ") ");
            EmitInlines(footnote.Content);
            _writer.EndBlock("p");
        }
        _writer.EndBlock("div");
    }

    // ----- inline content --------------------------------------------------------------------

    private void EmitInlines(IReadOnlyList<TexinfoNode> nodes)
    {
        foreach (TexinfoNode node in nodes)
        {
            EmitInline(node);
        }
    }

    /// <summary>
    /// Emits content somewhere other than where the document wrote it - a contents line, an index
    /// line - where the identifiers it carries have already been emitted at the real place. A
    /// second copy of an identifier would give the document two destinations of the same name.
    /// </summary>
    private void EmitRepeatedInlines(IReadOnlyList<TexinfoNode> nodes)
    {
        _repeatedDepth++;
        EmitInlines(nodes);
        _repeatedDepth--;
    }

    private void EmitInline(TexinfoNode node)
    {
        FlushNestedIndentBefore(node);
        switch (node)
        {
            case TextNode text:
                WriteText(text.Text);
                return;
            case GlyphNode glyph:
                _writer.Text(glyph.Text);
                return;
            case LineBreakNode:
                _writer.BeginInline("br");
                _writer.CloseStartTag();
                return;
            case InlineCommandNode command:
                EmitInlineCommand(command);
                return;
            case AcronymNode acronym:
                EmitAcronym(acronym);
                return;
            case CrossReferenceNode reference:
                EmitCrossReference(reference);
                return;
            case LinkNode link:
                EmitLink(link);
                return;
            case FootnoteNode footnote:
                EmitFootnoteMarker(footnote);
                return;
            case AnchorNode anchor:
                EmitInlineAnchor(_semantics.ElementIdFor(anchor.Name));
                return;
            case ImageNode image:
                EmitImage(image, asBlock: false);
                return;
            case MusicSnippetNode snippet:
                EmitMusicSnippet(snippet, asBlock: false);
                return;
            case PreformattedNode nested:
                EmitNestedPreformatted(nested);
                return;
            case VerbatimNode verbatim:
                _writer.BeginInline("code");
                _writer.CloseStartTag();
                _writer.Text(verbatim.Text);
                _writer.EndInline("code");
                return;
            case IndexEntryNode entry:
                //An index entry marks a place; the index itself is what renders its text.
                EmitInlineAnchor(_semantics.IndexEntryIdFor(entry));
                return;
            default:
                EmitInlines(Children(node));
                return;
        }
    }

    private static IReadOnlyList<TexinfoNode> Children(TexinfoNode node)
    {
        List<TexinfoNode> children = new List<TexinfoNode>();
        foreach (TexinfoNode child in node.ChildNodes)
        {
            children.Add(child);
        }
        return children;
    }

    /// <summary>
    /// Reports, once for the document, that mathematics was set as ordinary text. There is no
    /// mathematical typesetter here and there is not going to be one, so this is a statement of
    /// what the reader is getting rather than a gap waiting to be filled.
    /// </summary>
    private void WarnAboutMathematics(SourcePosition position)
    {
        if (_warnedAboutMath)
        {
            return;
        }
        _warnedAboutMath = true;
        _warnings.Add(TexinfoWarningCategory.Emit, position,
            "'@math' was rendered as styled text; this library has no mathematical typesetter.");
    }

    private void EmitInlineCommand(InlineCommandNode command)
    {
        if (command.Style == InlineStyle.Math)
        {
            WarnAboutMathematics(command.Position);
        }
        if (command.Style == InlineStyle.SortAs)
        {
            //A sort key for the index entry around it, never visible text.
            return;
        }
        EmitStyled(command.Style, command.Content);
    }

    private void EmitStyled(InlineStyle style, IReadOnlyList<TexinfoNode> content)
    {
        string tag;
        string cssClass;
        switch (style)
        {
            case InlineStyle.Code:
            case InlineStyle.FileName:
            case InlineStyle.CommandName:
            case InlineStyle.Option:
            case InlineStyle.EnvironmentVariable:
                tag = "code";
                cssClass = null;
                break;
            case InlineStyle.Sample:
                tag = "samp";
                cssClass = null;
                break;
            case InlineStyle.Keyboard:
                tag = "kbd";
                cssClass = null;
                break;
            case InlineStyle.Key:
                tag = "kbd";
                cssClass = "texinfo-key";
                break;
            case InlineStyle.Emphasis:
            case InlineStyle.Definition:
                tag = "em";
                cssClass = null;
                break;
            case InlineStyle.Strong:
                tag = "strong";
                cssClass = null;
                break;
            case InlineStyle.Bold:
                tag = "b";
                cssClass = null;
                break;
            case InlineStyle.Italic:
            case InlineStyle.Slanted:
            case InlineStyle.Citation:
                tag = "i";
                cssClass = null;
                break;
            case InlineStyle.Variable:
                tag = "i";
                cssClass = "texinfo-var";
                break;
            case InlineStyle.Math:
                tag = "i";
                cssClass = "texinfo-math";
                break;
            case InlineStyle.Typewriter:
                tag = "span";
                cssClass = "texinfo-t";
                break;
            case InlineStyle.Roman:
                tag = "span";
                cssClass = "texinfo-r";
                break;
            case InlineStyle.SansSerif:
                tag = "span";
                cssClass = "texinfo-sansserif";
                break;
            case InlineStyle.SmallCaps:
                tag = "span";
                cssClass = "texinfo-sc";
                break;
            case InlineStyle.TitleFont:
                tag = "span";
                cssClass = "texinfo-titlefont";
                break;
            case InlineStyle.IndicateUrl:
                tag = "span";
                cssClass = "texinfo-url";
                break;
            case InlineStyle.Superscript:
                tag = "sup";
                cssClass = null;
                break;
            case InlineStyle.Subscript:
                tag = "sub";
                cssClass = null;
                break;
            case InlineStyle.SubEntry:
                //A second-level index entry reads as a continuation of the entry it hangs from.
                _writer.Text(", ");
                EmitInlines(content);
                return;
            case InlineStyle.SeeEntry:
                EmitIndexRedirect("see ", content);
                return;
            case InlineStyle.SeeAlso:
                EmitIndexRedirect("see also ", content);
                return;
            case InlineStyle.NoBreak:
                //@w asks that its content not be broken across lines, and a space that cannot be
                //broken at is the only way to say so in the output subset.
                _noBreakDepth++;
                EmitInlines(content);
                _noBreakDepth--;
                return;
            case InlineStyle.Dimension:
                //@dmn sets a unit of measure off from the number in front of it, without letting a
                //line break come between the two.
                _writer.Raw("&#160;");
                EmitInlines(content);
                return;
            default:
                //@asis and @clicksequence add no appearance of their own; their content stands on
                //its own.
                EmitInlines(content);
                return;
        }
        //@samp is printed inside single quotation marks, and they belong to the surrounding text
        //rather than to the sample, so they are written outside the element.
        bool quoted = style == InlineStyle.Sample;
        if (quoted)
        {
            _writer.Text("‘");
        }
        bool literal = IsLiteralStyle(style);
        _writer.BeginInline(tag);
        _writer.Attribute("class", cssClass);
        _writer.CloseStartTag();
        if (literal)
        {
            _literalDepth++;
        }
        EmitInlines(content);
        if (literal)
        {
            _literalDepth--;
        }
        _writer.EndInline(tag);
        if (quoted)
        {
            _writer.Text("’");
        }
    }

    /// <summary>
    /// Writes an <c>@acronym</c> or <c>@abbr</c>. The short form is what the sentence reads; the
    /// words behind it, when the document gave them, follow in parentheses, which is the
    /// convention every printed manual uses and the one Texinfo's own output follows.
    /// </summary>
    private void EmitAcronym(AcronymNode acronym)
    {
        if (acronym.IsAcronym)
        {
            EmitStyled(InlineStyle.SmallCaps, acronym.ShortForm);
        }
        else
        {
            EmitInlines(acronym.ShortForm);
        }
        if (acronym.Meaning.Count > 0)
        {
            _writer.Text(" (");
            EmitInlines(acronym.Meaning);
            _writer.Text(")");
        }
    }

    private void EmitIndexRedirect(string wording, IReadOnlyList<TexinfoNode> content)
    {
        //An index entry that redirects to another one prints its 'see' in the body font, italic by
        //the convention every printed index follows, and the entry it points at after it.
        _writer.BeginInline("i");
        _writer.CloseStartTag();
        _writer.Text(wording);
        _writer.EndInline("i");
        EmitInlines(content);
    }

    private static bool IsLiteralStyle(InlineStyle style)
    {
        //The code-like commands: what they wrap is a literal sequence of characters, so a run of
        //hyphens in an option name and an apostrophe in a shell command survive as written.
        switch (style)
        {
            case InlineStyle.Code:
            case InlineStyle.CommandName:
            case InlineStyle.EnvironmentVariable:
            case InlineStyle.FileName:
            case InlineStyle.IndicateUrl:
            case InlineStyle.Key:
            case InlineStyle.Keyboard:
            case InlineStyle.Option:
            case InlineStyle.Sample:
            case InlineStyle.Typewriter:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Indents each line of text written inside a nested preformatted block. A line is indented
    /// when its first character arrives, so a line that turns out to be empty stays empty.
    /// </summary>
    private string ApplyNestedIndent(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }
        string indent = NestedIndentFor(_nestedPreformattedDepth);
        StringBuilder builder = new StringBuilder(text.Length + indent.Length);
        foreach (char c in text)
        {
            if (c == '\n')
            {
                _nestedIndentPending = true;
                builder.Append(c);
                continue;
            }
            if (_nestedIndentPending)
            {
                builder.Append(indent);
                _nestedIndentPending = false;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }

    private static string NestedIndentFor(int depth)
    {
        if (depth <= 1)
        {
            return NestedPreformattedIndent;
        }
        StringBuilder indent = new StringBuilder(NestedPreformattedIndent.Length * depth);
        for (int level = 0; level < depth; level++)
        {
            indent.Append(NestedPreformattedIndent);
        }
        return indent.ToString();
    }

    private void WriteText(string text)
    {
        string result = _literalDepth > 0 ? text : TextConventions.Apply(text);
        if (_nestedPreformattedDepth > 0)
        {
            result = ApplyNestedIndent(result);
        }
        _writer.Text(_noBreakDepth > 0 ? result.Replace(' ', ' ') : result);
    }

    private void EmitCrossReference(CrossReferenceNode reference)
    {
        //Texinfo prescribes the wording: @xref opens a sentence and is capitalized, @pxref is
        //parenthetical and is not, and @ref supplies nothing of its own.
        switch (reference.Kind)
        {
            case CrossReferenceKind.SentenceStart:
            case CrossReferenceKind.InfoReference:
                _writer.Text("See ");
                break;
            case CrossReferenceKind.Parenthetical:
                _writer.Text("see ");
                break;
        }
        //A reference into another manual has no destination in this document, and inventing one
        //would be worse than leaving the reader with the manual's name, which is what follows.
        string elementId = reference.IsExternal
            ? string.Empty
            : _semantics.ElementIdFor(reference.NodeName);
        if (elementId.Length == 0 && !reference.IsExternal)
        {
            NoteUnresolvedReference(reference);
        }
        if (elementId.Length > 0)
        {
            _writer.BeginInline("a");
            _writer.Attribute("href", "#" + elementId);
            _writer.CloseStartTag();
        }
        if (reference.Title.Count > 0)
        {
            EmitInlines(reference.Title);
        }
        else if (reference.Label.Length > 0)
        {
            WriteText(reference.Label);
        }
        else
        {
            //A float is addressed by a label such as 'fig:staff-sizes', which tells a reader
            //nothing; its type and number are what the reference is meant to read as.
            string floatText = _semantics.ReferenceTextFor(reference.NodeName);
            WriteText(floatText.Length > 0 ? floatText : reference.NodeName);
        }
        if (elementId.Length > 0)
        {
            _writer.EndInline("a");
        }
        if (reference.Manual.Length > 0)
        {
            _writer.Text(" in ");
            _writer.BeginInline("i");
            _writer.CloseStartTag();
            WriteText(reference.Manual);
            _writer.EndInline("i");
        }
    }

    private void NoteUnresolvedReference(CrossReferenceNode reference)
    {
        _unresolvedReferenceCount++;
        if (_unresolvedReferenceCount > 1)
        {
            return;
        }
        _firstUnresolvedReference = reference.NodeName;
        _firstUnresolvedPosition = reference.Position;
    }

    private void ReportUnresolvedReferences()
    {
        if (_unresolvedReferenceCount == 0)
        {
            return;
        }
        //One message for the lot: a manual that has lost an included file loses every reference
        //into it at once, and a thousand identical warnings would bury everything else.
        _warnings.Add(TexinfoWarningCategory.Reference, _firstUnresolvedPosition,
            $"{_unresolvedReferenceCount.ToString(CultureInfo.InvariantCulture)} cross reference(s) "
            + "name a destination this document does not define, starting with "
            + $"'{_firstUnresolvedReference}'; they were rendered as text without a link.");
    }

    private void EmitLink(LinkNode link)
    {
        //A third argument replaces the reference outright: Texinfo says the URL is then not output
        //in any format and the second argument is ignored, which is how a manual writes a reference
        //that is already sufficiently referential in its own words.
        if (link.Replacement.Length > 0)
        {
            WriteText(link.Replacement);
            return;
        }
        string href = link.Kind == LinkKind.Email
            ? (link.Target.Length > 0 ? "mailto:" + link.Target : string.Empty)
            : link.Target;
        _writer.BeginInline("a");
        _writer.Attribute("href", href);
        _writer.CloseStartTag();
        if (link.Text.Count > 0)
        {
            EmitInlines(link.Text);
        }
        else
        {
            //The URL stands as its own visible text, and it is a literal one: no run of hyphens in
            //it is an en dash.
            _writer.Text(link.Target);
        }
        _writer.EndInline("a");
    }

    private void EmitFootnoteMarker(FootnoteNode footnote)
    {
        string id = _semantics.FootnoteIdFor(footnote);
        _writer.BeginInline("sup");
        _writer.Attribute("class", "texinfo-footnote-ref");
        _writer.CloseStartTag();
        _writer.BeginInline("a");
        _writer.Attribute("href", id.Length > 0 ? "#" + id : null);
        _writer.CloseStartTag();
        _writer.Text(footnote.Number.ToString(CultureInfo.InvariantCulture));
        _writer.EndInline("a");
        _writer.EndInline("sup");
    }

    private void EmitInlineAnchor(string elementId)
    {
        if (elementId.Length == 0 || _repeatedDepth > 0)
        {
            return;
        }
        _writer.BeginInline("span");
        _writer.Attribute("id", elementId);
        _writer.CloseStartTag();
        _writer.EndInline("span");
    }

    // ----- images and music snippets ---------------------------------------------------------

    private void EmitImage(ImageNode image, bool asBlock)
    {
        if (!_images.TryResolve(image.FileName, image.Extension, out string source))
        {
            _warnings.Add(TexinfoWarningCategory.Include, image.Position,
                $"Image '{image.FileName}' was not found on the search path; its alternate text was "
                + "used instead.");
            string alternate = image.AlternateText.Length > 0 ? image.AlternateText : image.FileName;
            if (asBlock)
            {
                _writer.BeginBlock("p");
                _writer.Attribute("class", "texinfo-missing-image");
                _writer.CloseStartTag();
                _writer.Text("[" + alternate + "]");
                _writer.EndBlock("p");
                return;
            }
            _writer.BeginInline("span");
            _writer.Attribute("class", "texinfo-missing-image");
            _writer.CloseStartTag();
            _writer.Text("[" + alternate + "]");
            _writer.EndInline("span");
            return;
        }
        string style = LengthStyle("width", image.Width);
        if (style.Length == 0)
        {
            style = LengthStyle("height", image.Height);
        }
        if (asBlock)
        {
            _writer.BeginVoidBlock("img");
        }
        else
        {
            _writer.BeginInline("img");
        }
        _writer.Attribute("src", source);
        _writer.Attribute("alt", image.AlternateText);
        _writer.Attribute("class", "texinfo-image");
        _writer.Attribute("style", style.Length > 0 ? style : null);
        _writer.CloseStartTag();
    }

    private static string LengthStyle(string property, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        string text = value.Trim();
        int digits = 0;
        while (digits < text.Length && (char.IsDigit(text[digits]) || text[digits] == '.'))
        {
            digits++;
        }
        if (digits == 0)
        {
            return string.Empty;
        }
        string unit = text.Substring(digits).Trim().ToLowerInvariant();
        switch (unit)
        {
            case "pt":
            case "px":
            case "in":
            case "cm":
            case "mm":
            case "pc":
            case "em":
            case "%":
                return property + ": " + text.Substring(0, digits) + unit;
            default:
                //TeX dimensions such as "0.5\textwidth" have no counterpart in the CSS subset.
                return string.Empty;
        }
    }

    private void EmitMusicSnippet(MusicSnippetNode snippet, bool asBlock)
    {
        PreparedSnippet prepared = _snippets.Prepare(snippet);
        //'quote' asks for the snippet to stand in from the margin. An inline style says it because
        //a bordered container would turn the snippet into a box, and Html2Pdf lays a box out as a
        //unit - which is the trap that swallowed multitables inside @quotation.
        string indent = asBlock && prepared.Options.Quote ? "margin-left: 2em" : null;
        if (prepared.ShowSource)
        {
            //The source comes first and the engraving under it, which is the order a reader of a
            //manual expects: this is the input, this is what it produces.
            EmitSnippetSource(prepared.SourceText, asBlock, indent);
        }
        string alternate = AlternateTextFor(snippet, prepared);
        foreach (string path in prepared.ImagePaths)
        {
            if (asBlock)
            {
                _writer.BeginVoidBlock("img");
            }
            else
            {
                _writer.BeginInline("img");
            }
            _writer.Attribute("src", path);
            _writer.Attribute("alt", alternate);
            _writer.Attribute("class", "texinfo-lilypond-image");
            _writer.Attribute("style", indent);
            _writer.CloseStartTag();
        }
    }

    private void EmitSnippetSource(string text, bool asBlock, string indent)
    {
        if (asBlock)
        {
            _writer.BeginBlock("pre");
            _writer.Attribute("class", "texinfo-lilypond");
            _writer.Attribute("style", indent);
            _writer.CloseStartTag();
            _writer.BeginPreformatted();
            _writer.Text(text);
            _writer.EndBlock("pre");
            _writer.EndPreformatted();
            return;
        }
        _writer.BeginInline("code");
        _writer.Attribute("class", "texinfo-lilypond");
        _writer.CloseStartTag();
        _writer.Text(text);
        _writer.EndInline("code");
    }

    private static string AlternateTextFor(MusicSnippetNode snippet, PreparedSnippet prepared)
    {
        if (snippet.IsFileReference)
        {
            return snippet.Content.Trim();
        }
        //The first line of the music is the closest thing a snippet has to a name.
        foreach (string line in prepared.SourceText.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed.Length <= 80 ? trimmed : trimmed.Substring(0, 77) + "...";
            }
        }
        return "music";
    }
}
