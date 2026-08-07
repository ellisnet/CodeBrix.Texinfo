using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Parsing;
using CodeBrix.Texinfo2Html.Semantics;
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
    private readonly TexinfoDocument _document;
    private readonly DocumentSemantics _semantics;
    private readonly ImageReferenceResolver _images;
    private readonly TexinfoWarningCollection _warnings;
    private readonly HtmlWriter _writer = new HtmlWriter();
    private int _musicSnippetCount;
    private SourcePosition _firstMusicSnippet;
    private bool _warnedAboutMath;
    private bool _warnedAboutIndex;
    private bool _titlePageEmitted;

    /// <summary>Creates an emitter for one analyzed document.</summary>
    /// <param name="document">The parsed document.</param>
    /// <param name="semantics">The results of the semantic passes over that document.</param>
    /// <param name="images">Resolver used to turn image references into paths on disk.</param>
    public HtmlEmitter(TexinfoDocument document, DocumentSemantics semantics,
        ImageReferenceResolver images)
    {
        _document = document;
        _semantics = semantics;
        _images = images;
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
        EmitFootnotes();
        ReportMusicSnippets();
        return _writer.ToString();
    }

    // ----- sectioning ------------------------------------------------------------------------

    private void EmitSection(SectionNode section)
    {
        string tag = "h" + (section.HeadingLevel < 1 ? 1 : section.HeadingLevel)
            .ToString(CultureInfo.InvariantCulture);
        _writer.BeginBlock(tag);
        _writer.Attribute("id", section.ElementId);
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
            case MenuNode:
            case IndexEntryNode:
                //Menus are navigation for the Info reader, and index entries are markers that the
                //index itself renders; neither is content in a printed document.
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
        string cssClass = paragraph.Alignment == ParagraphAlignment.Centered
            ? "texinfo-center"
            : paragraph.SuppressIndent ? "texinfo-noindent" : string.Empty;
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
        _writer.BeginBlock("pre");
        _writer.Attribute("class", PreformattedClass(preformatted.Kind));
        _writer.CloseStartTag();
        _writer.BeginPreformatted();
        EmitInlines(preformatted.Content);
        _writer.EndBlock("pre");
        _writer.EndPreformatted();
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
                if (!_warnedAboutIndex)
                {
                    _warnedAboutIndex = true;
                    _warnings.Add(TexinfoWarningCategory.Emit, directive.Position,
                        "'@printindex' is not rendered yet; the index was left out of the document.");
                }
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
            EmitInlines(entry.Section.Title);
            _writer.EndInline("a");
            _writer.EndBlock("p");
        }
        _writer.EndBlock("div");
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
        if (elementId.Length == 0)
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

    private void EmitFootnotes()
    {
        if (_document.Footnotes.Count == 0)
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
        foreach (FootnoteNode footnote in _document.Footnotes)
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

    private void EmitInline(TexinfoNode node)
    {
        switch (node)
        {
            case TextNode text:
                _writer.Text(text.Text);
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
            case VerbatimNode verbatim:
                _writer.BeginInline("code");
                _writer.CloseStartTag();
                _writer.Text(verbatim.Text);
                _writer.EndInline("code");
                return;
            case IndexEntryNode:
                //An index entry marks a place; the index itself is what renders it.
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

    private void EmitInlineCommand(InlineCommandNode command)
    {
        if (command.Style == InlineStyle.Math && !_warnedAboutMath)
        {
            _warnedAboutMath = true;
            _warnings.Add(TexinfoWarningCategory.Emit, command.Position,
                "'@math' was rendered as styled text; this library has no mathematical typesetter.");
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
            default:
                //@asis, @w, @dmn, @clicksequence, @subentry and the see-entry commands add no
                //appearance of their own; their content stands on its own.
                EmitInlines(content);
                return;
        }
        _writer.BeginInline(tag);
        _writer.Attribute("class", cssClass);
        _writer.CloseStartTag();
        EmitInlines(content);
        _writer.EndInline(tag);
    }

    private void EmitCrossReference(CrossReferenceNode reference)
    {
        //Wave 4 turns these into real links. For now the reference reads correctly even though it
        //does not yet jump: the wording Texinfo prescribes, followed by the destination's name.
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
        if (reference.Title.Count > 0)
        {
            EmitInlines(reference.Title);
        }
        else if (reference.Label.Length > 0)
        {
            _writer.Text(reference.Label);
        }
        else
        {
            _writer.Text(reference.NodeName);
        }
        if (reference.Manual.Length > 0)
        {
            _writer.Text(" in ");
            _writer.BeginInline("i");
            _writer.CloseStartTag();
            _writer.Text(reference.Manual);
            _writer.EndInline("i");
        }
    }

    private void EmitLink(LinkNode link)
    {
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
        else if (link.Replacement.Length > 0)
        {
            _writer.Text(link.Replacement);
        }
        else
        {
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
        if (elementId.Length == 0)
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
        if (!_images.TryResolve(image.FileName, image.Extension, out string path))
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
        _writer.Attribute("src", path);
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
        if (_musicSnippetCount == 0)
        {
            _firstMusicSnippet = snippet.Position;
        }
        _musicSnippetCount++;
        string text = snippet.IsFileReference
            ? "@" + snippet.CommandName + " " + snippet.Content
            : snippet.Content;
        if (asBlock)
        {
            _writer.BeginBlock("pre");
            _writer.Attribute("class", "texinfo-lilypond");
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

    private void ReportMusicSnippets()
    {
        if (_musicSnippetCount == 0)
        {
            return;
        }
        _warnings.Add(TexinfoWarningCategory.Emit, _firstMusicSnippet,
            $"{_musicSnippetCount.ToString(CultureInfo.InvariantCulture)} music snippet(s) were "
            + "emitted as their source text; no snippet renderer is available to engrave them.");
    }
}
