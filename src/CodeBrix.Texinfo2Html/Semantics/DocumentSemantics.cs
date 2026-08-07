using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Parsing;

namespace CodeBrix.Texinfo2Html.Semantics;

/// <summary>
/// The pass that turns a parsed document into an emittable one. The parser records only what the
/// source said; this stage works out what the source meant across the whole document - section
/// numbers, the identifier every heading and anchor is addressed by, the heading rank each unit
/// renders at, the table of contents, and the identifiers footnotes are linked through.
/// </summary>
/// <remarks>
/// It is deliberately separate from both the parser and the emitter: numbering and identifier
/// allocation need the finished tree, and an emitter that computed them as it walked would have to
/// emit a forward reference to a number it had not reached yet.
/// </remarks>
internal sealed class DocumentSemantics
{
    private readonly ElementIdAllocator _allocator = new ElementIdAllocator();
    private readonly List<TableOfContentsEntry> _contents = new List<TableOfContentsEntry>();
    private readonly List<TableOfContentsEntry> _shortContents = new List<TableOfContentsEntry>();
    private readonly Dictionary<FootnoteNode, string> _footnoteIds =
        new Dictionary<FootnoteNode, string>();
    private readonly TexinfoDocument _document;
    private readonly bool _numberSections;
    private int _chapterCounter;
    private int _appendixCounter;

    private DocumentSemantics(TexinfoDocument document, bool numberSections)
    {
        _document = document;
        _numberSections = numberSections;
    }

    /// <summary>Runs every semantic pass over the document and returns the results.</summary>
    /// <param name="document">The parsed document; its sectioning units and anchors are filled in.</param>
    /// <param name="numberSections">False to leave every section unnumbered.</param>
    public static DocumentSemantics Analyze(TexinfoDocument document, bool numberSections)
    {
        DocumentSemantics semantics = new DocumentSemantics(document, numberSections);
        semantics.Run();
        return semantics;
    }

    /// <summary>The table of contents, one entry per sectioning unit below the topmost one.</summary>
    public IReadOnlyList<TableOfContentsEntry> Contents => _contents;

    /// <summary>The short table of contents, holding only chapter-level units.</summary>
    public IReadOnlyList<TableOfContentsEntry> ShortContents => _shortContents;

    /// <summary>
    /// The document's <c>@titlepage</c> block, or null when it has none. A printed manual opens
    /// with its title page wherever the command was written, so the emitter hoists this block to
    /// the front and skips it where it stands.
    /// </summary>
    public BlockEnvironmentNode TitlePage { get; private set; }

    /// <summary>Looks up the identifier a footnote's text is emitted under.</summary>
    /// <param name="footnote">The footnote to look up.</param>
    public string FootnoteIdFor(FootnoteNode footnote)
        => _footnoteIds.TryGetValue(footnote, out string id) ? id : string.Empty;

    /// <summary>
    /// Looks up the identifier a cross reference to the given Texinfo name should target, or an
    /// empty string when the document defines no such destination.
    /// </summary>
    /// <param name="name">A <c>@node</c> or <c>@anchor</c> name.</param>
    public string ElementIdFor(string name)
        => name != null && _document.Anchors.TryGetValue(name, out TexinfoAnchor anchor)
            ? anchor.ElementId
            : string.Empty;

    private void Run()
    {
        NumberAndIdentify(_document.Sections, string.Empty, depth: 0);
        AssignRemainingAnchorIds();
        AssignFootnoteIds();
        BuildContents(_document.Sections, depth: 0);
        FindTitlePage();
    }

    private void FindTitlePage()
    {
        foreach (TexinfoNode node in _document.AllNodes())
        {
            if (node is BlockEnvironmentNode environment
                && environment.Kind == TexinfoBlockKind.TitlePage)
            {
                TitlePage = environment;
                return;
            }
        }
    }

    private void NumberAndIdentify(IReadOnlyList<SectionNode> sections, string parentNumber, int depth)
    {
        int siblingCounter = 0;
        foreach (SectionNode section in sections)
        {
            section.HeadingLevel = depth + 1 > 6 ? 6 : depth + 1;
            section.Number = NextNumber(section, parentNumber, ref siblingCounter);
            section.ElementId = _allocator.Allocate(section.NodeName.Length > 0
                ? section.NodeName
                : InlineNodes.ToPlainText(section.Title));
            NumberAndIdentify(section.Children, section.Number, depth + 1);
        }
    }

    private string NextNumber(SectionNode section, string parentNumber, ref int siblingCounter)
    {
        if (!_numberSections
            || section.Kind == SectionKind.Top
            || section.Kind == SectionKind.Part
            || section.Kind == SectionKind.Unnumbered)
        {
            return string.Empty;
        }
        //Chapter-level units draw on document-wide counters so that chapters keep counting across
        //an intervening @part, and appendices run A, B, C independently of them.
        if (section.Level <= 1)
        {
            if (section.Kind == SectionKind.Appendix)
            {
                _appendixCounter++;
                return AppendixLetter(_appendixCounter);
            }
            _chapterCounter++;
            return _chapterCounter.ToString(CultureInfo.InvariantCulture);
        }
        //A unit below an unnumbered one stays unnumbered: there is no stem to hang a number on.
        if (parentNumber.Length == 0)
        {
            return string.Empty;
        }
        siblingCounter++;
        return parentNumber + "." + siblingCounter.ToString(CultureInfo.InvariantCulture);
    }

    private static string AppendixLetter(int value)
    {
        StringBuilder builder = new StringBuilder();
        while (value > 0)
        {
            value--;
            builder.Insert(0, (char)('A' + value % 26));
            value /= 26;
        }
        return builder.ToString();
    }

    private void AssignRemainingAnchorIds()
    {
        //Anchors that name a sectioning unit share that unit's identifier, so a cross reference
        //lands on the heading rather than on an invisible marker beside it. Every other
        //destination gets its own, allocated in document order for a stable result.
        foreach (TexinfoAnchor anchor in _document.Anchors.Values)
        {
            if (anchor.Target is SectionNode section)
            {
                anchor.ElementId = section.ElementId;
            }
        }
        foreach (TexinfoNode node in _document.AllNodes())
        {
            string name = node switch
            {
                AnchorNode anchorNode => anchorNode.Name,
                NodeAnchorNode nodeAnchor => nodeAnchor.NodeName,
                _ => null
            };
            if (name == null || !_document.Anchors.TryGetValue(name, out TexinfoAnchor anchor))
            {
                continue;
            }
            if (anchor.Target == node && anchor.ElementId.Length == 0)
            {
                anchor.ElementId = _allocator.Allocate(name);
            }
        }
    }

    private void AssignFootnoteIds()
    {
        foreach (FootnoteNode footnote in _document.Footnotes)
        {
            _footnoteIds[footnote] = _allocator.Allocate(
                "footnote-" + footnote.Number.ToString(CultureInfo.InvariantCulture));
        }
    }

    private void BuildContents(IReadOnlyList<SectionNode> sections, int depth)
    {
        foreach (SectionNode section in sections)
        {
            //@top names the document as a whole; listing it in its own contents helps nobody.
            if (section.Kind == SectionKind.Top)
            {
                BuildContents(section.Children, depth);
                continue;
            }
            _contents.Add(new TableOfContentsEntry(section, depth));
            if (section.Level <= 1)
            {
                _shortContents.Add(new TableOfContentsEntry(section, 0));
            }
            BuildContents(section.Children, depth + 1);
        }
    }
}
