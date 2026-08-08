using System;
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
    private readonly Dictionary<SectionNode, List<FootnoteNode>> _footnotesBySection =
        new Dictionary<SectionNode, List<FootnoteNode>>();
    private readonly List<FootnoteNode> _trailingFootnotes = new List<FootnoteNode>();
    private readonly TexinfoDocument _document;
    private IndexBuilder _indexes;
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
    /// The footnotes belonging to one sectioning unit, printed at the end of it. Empty for a unit
    /// that hosts none.
    /// </summary>
    /// <param name="section">The sectioning unit to look up.</param>
    public IReadOnlyList<FootnoteNode> FootnotesFor(SectionNode section)
        => _footnotesBySection.TryGetValue(section, out List<FootnoteNode> notes)
            ? notes
            : Array.Empty<FootnoteNode>();

    /// <summary>
    /// The footnotes no sectioning unit hosts - those written in the front matter, or in a document
    /// with no sectioning at all. They are printed at the end of the document.
    /// </summary>
    public IReadOnlyList<FootnoteNode> TrailingFootnotes => _trailingFootnotes;

    /// <summary>
    /// Looks up a printed index by the name <c>@printindex</c> gave, or null when the document
    /// never asks to print an index of that name.
    /// </summary>
    /// <param name="name">The two-letter index name.</param>
    public PrintedIndex IndexNamed(string name)
        => name != null && _indexes.Indexes.TryGetValue(name, out PrintedIndex index) ? index : null;

    /// <summary>
    /// Looks up the identifier of the marker a printed index links back to, or an empty string for
    /// an entry that no printed index contains.
    /// </summary>
    /// <param name="entry">The index entry to look up.</param>
    public string IndexEntryIdFor(IndexEntryNode entry)
        => entry != null && _indexes.EntryIds.TryGetValue(entry, out string id) ? id : string.Empty;

    /// <summary>
    /// Looks up the identifier a cross reference to the given Texinfo name should target, or an
    /// empty string when the document defines no such destination.
    /// </summary>
    /// <param name="name">A <c>@node</c> or <c>@anchor</c> name.</param>
    public string ElementIdFor(string name)
        => name != null && _document.Anchors.TryGetValue(name, out TexinfoAnchor anchor)
            ? anchor.ElementId
            : string.Empty;

    /// <summary>
    /// How a cross reference to the given name reads when the document supplies no wording of its
    /// own. Only a float answers: "see Figure 1.2" is what its number is for, and a label such as
    /// <c>fig:staff-sizes</c> would tell a reader nothing. Empty for every other destination, whose
    /// own node name is already the right words.
    /// </summary>
    /// <param name="name">A <c>@float</c> label.</param>
    public string ReferenceTextFor(string name)
        => name != null && _document.Anchors.TryGetValue(name, out TexinfoAnchor anchor)
           && anchor.Target is FloatNode target
            ? target.ReferenceText
            : string.Empty;

    /// <summary>
    /// The document's floats of one type, in the order they were written - what
    /// <c>@listoffloats</c> prints. An empty type name asks for every float.
    /// </summary>
    /// <param name="typeName">The float type, such as "Figure".</param>
    public IReadOnlyList<FloatNode> FloatsOfType(string typeName)
    {
        List<FloatNode> found = new List<FloatNode>();
        foreach (FloatNode node in _document.Floats)
        {
            if (typeName.Length == 0
                || string.Equals(node.TypeName, typeName, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(node);
            }
        }
        return found;
    }

    private void Run()
    {
        NumberAndIdentify(_document.Sections, string.Empty, depth: 0);
        NumberFloats();
        AssignRemainingAnchorIds();
        AssignFootnoteIds();
        PlaceFootnotes();
        _indexes = new IndexBuilder(_document, _allocator);
        _indexes.Build();
        BuildContents(_document.Sections, depth: 0);
        FindTitlePage();
    }

    /// <summary>
    /// Works out where each footnote's text is printed. A printed manual puts its notes at the end
    /// of the chapter they were written in, so each footnote is filed under the outermost
    /// sectioning unit that contains it - skipping <c>@top</c> and <c>@part</c>, which are wrappers
    /// around chapters rather than chapters themselves.
    /// </summary>
    private void PlaceFootnotes()
    {
        HashSet<FootnoteNode> hosted = new HashSet<FootnoteNode>();
        foreach (SectionNode section in _document.Sections)
        {
            FindFootnoteHosts(section, hosted);
        }
        foreach (FootnoteNode footnote in _document.Footnotes)
        {
            if (!hosted.Contains(footnote))
            {
                _trailingFootnotes.Add(footnote);
            }
        }
    }

    private void FindFootnoteHosts(SectionNode section, HashSet<FootnoteNode> hosted)
    {
        if (section.Kind == SectionKind.Top || section.Kind == SectionKind.Part)
        {
            foreach (SectionNode child in section.Children)
            {
                FindFootnoteHosts(child, hosted);
            }
            return;
        }
        List<FootnoteNode> notes = new List<FootnoteNode>();
        foreach (TexinfoNode node in section.DescendantNodes())
        {
            if (node is FootnoteNode footnote && hosted.Add(footnote))
            {
                notes.Add(footnote);
            }
        }
        if (notes.Count > 0)
        {
            _footnotesBySection[section] = notes;
        }
    }

    /// <summary>
    /// Numbers the document's floats. A float is numbered within its chapter and within its own
    /// type, so a manual counts "Figure 1.1, Figure 1.2, Table 1.1" and starts again at "Figure
    /// 2.1" in the next chapter. A float whose chapter carries no number - and one written before
    /// any chapter at all - has no stem to hang that on, so it counts straight through the document
    /// instead.
    /// </summary>
    private void NumberFloats()
    {
        if (_document.Floats.Count == 0)
        {
            return;
        }
        Dictionary<string, int> documentCounters =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        HashSet<FloatNode> numbered = new HashSet<FloatNode>();
        NumberFloatsInChapters(_document.Sections, documentCounters, numbered);
        foreach (FloatNode node in _document.Floats)
        {
            if (numbered.Add(node))
            {
                node.Number = NextFloatCount(documentCounters, node.TypeName)
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    private void NumberFloatsInChapters(IReadOnlyList<SectionNode> sections,
        Dictionary<string, int> documentCounters, HashSet<FloatNode> numbered)
    {
        foreach (SectionNode section in sections)
        {
            //@top and @part group chapters rather than being one, so they are passed through: the
            //counters restart at the chapter, which is what supplies the number they build on.
            if (section.Kind == SectionKind.Top || section.Kind == SectionKind.Part)
            {
                NumberFloatsInChapters(section.Children, documentCounters, numbered);
                continue;
            }
            Dictionary<string, int> chapterCounters =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (TexinfoNode node in section.DescendantNodes())
            {
                if (!(node is FloatNode target) || !numbered.Add(target))
                {
                    continue;
                }
                target.Number = section.Number.Length > 0
                    ? section.Number + "."
                      + NextFloatCount(chapterCounters, target.TypeName)
                          .ToString(CultureInfo.InvariantCulture)
                    : NextFloatCount(documentCounters, target.TypeName)
                        .ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    private static int NextFloatCount(Dictionary<string, int> counters, string typeName)
    {
        //Floats of different types count separately, and the ones the source gave no type share a
        //counter of their own rather than joining any of them.
        string key = typeName.Length == 0 ? " untyped" : typeName;
        counters.TryGetValue(key, out int count);
        count++;
        counters[key] = count;
        return count;
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
                FloatNode floatNode => floatNode.Label,
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
