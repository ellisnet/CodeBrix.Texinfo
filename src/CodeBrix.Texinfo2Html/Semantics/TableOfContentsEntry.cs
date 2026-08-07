using CodeBrix.Texinfo2Html.Model;

namespace CodeBrix.Texinfo2Html.Semantics;

/// <summary>
/// One line of a generated table of contents: the sectioning unit it names and how deeply it is
/// nested, so the emitter can indent it without walking the tree a second time.
/// </summary>
internal sealed class TableOfContentsEntry
{
    /// <summary>Creates a table-of-contents entry.</summary>
    /// <param name="section">The sectioning unit the entry points at.</param>
    /// <param name="depth">The entry's indent depth, starting at zero for chapter-level units.</param>
    public TableOfContentsEntry(SectionNode section, int depth)
    {
        Section = section;
        Depth = depth;
    }

    /// <summary>The sectioning unit the entry points at.</summary>
    public SectionNode Section { get; }

    /// <summary>The entry's indent depth, starting at zero for chapter-level units.</summary>
    public int Depth { get; }
}
