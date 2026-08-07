using System;
using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Diagnostics;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A parsed Texinfo document: its sectioning tree, the front matter that precedes it, and the
/// tables the parser gathered along the way so later passes need not walk the whole tree to find
/// anchors, index entries or footnotes. The parser is the only thing that fills an instance in;
/// every later pass treats the collections as read-only.
/// </summary>
internal sealed class TexinfoDocument
{
    /// <summary>Creates an empty document that shares the given warning collection.</summary>
    /// <param name="warnings">The collection every stage of the run appends to.</param>
    public TexinfoDocument(TexinfoWarningCollection warnings)
    {
        Warnings = warnings ?? new TexinfoWarningCollection();
    }

    /// <summary>The document title from <c>@settitle</c>; empty when none was given.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The language from <c>@documentlanguage</c>; empty when none was given.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>The encoding from <c>@documentencoding</c>; empty when none was given.</summary>
    public string Encoding { get; set; } = string.Empty;

    /// <summary>Content written before the first sectioning command: title page, copying, and so on.</summary>
    public List<TexinfoNode> Preamble { get; } = new List<TexinfoNode>();

    /// <summary>The topmost sectioning units, in document order.</summary>
    public List<SectionNode> Sections { get; } = new List<SectionNode>();

    /// <summary>
    /// The content of the <c>@copying</c> block. It is held here rather than left in the tree
    /// because Texinfo prints it where <c>@insertcopying</c> appears, not where it was written.
    /// </summary>
    public List<TexinfoNode> Copying { get; } = new List<TexinfoNode>();

    /// <summary>Every named destination in the document, keyed by the name a cross reference uses.</summary>
    public Dictionary<string, TexinfoAnchor> Anchors { get; } =
        new Dictionary<string, TexinfoAnchor>(StringComparer.Ordinal);

    /// <summary>Every index entry in the document, in the order it was written.</summary>
    public List<IndexEntryNode> IndexEntries { get; } = new List<IndexEntryNode>();

    /// <summary>Every footnote in the document, in the order it was written.</summary>
    public List<FootnoteNode> Footnotes { get; } = new List<FootnoteNode>();

    /// <summary>The index merges requested by <c>@syncodeindex</c> and <c>@synindex</c>.</summary>
    public List<IndexMerge> IndexMerges { get; } = new List<IndexMerge>();

    /// <summary>
    /// Document settings that are recorded but do not affect parsing, such as
    /// <c>@setchapternewpage</c> or <c>@paragraphindent</c>, keyed by command name without
    /// <c>@</c>. The emitter honors the ones it can and ignores the rest.
    /// </summary>
    public Dictionary<string, string> Settings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Every warning collected while lexing, preprocessing and parsing, in order.</summary>
    public TexinfoWarningCollection Warnings { get; }

    /// <summary>Every sectioning unit in the document, depth first and in document order.</summary>
    public IEnumerable<SectionNode> AllSections()
    {
        foreach (SectionNode section in Sections)
        {
            foreach (SectionNode result in Walk(section))
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Every node in the document, depth first and in document order, across the front matter,
    /// the sectioning tree and the copying block.
    /// </summary>
    public IEnumerable<TexinfoNode> AllNodes()
    {
        foreach (TexinfoNode node in Concat(Preamble, Copying))
        {
            yield return node;
            foreach (TexinfoNode descendant in node.DescendantNodes())
            {
                yield return descendant;
            }
        }
        foreach (SectionNode section in Sections)
        {
            yield return section;
            foreach (TexinfoNode descendant in section.DescendantNodes())
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<TexinfoNode> Concat(List<TexinfoNode> first, List<TexinfoNode> second)
    {
        foreach (TexinfoNode node in first)
        {
            yield return node;
        }
        foreach (TexinfoNode node in second)
        {
            yield return node;
        }
    }

    private static IEnumerable<SectionNode> Walk(SectionNode section)
    {
        yield return section;
        foreach (SectionNode child in section.Children)
        {
            foreach (SectionNode result in Walk(child))
            {
                yield return result;
            }
        }
    }
}
