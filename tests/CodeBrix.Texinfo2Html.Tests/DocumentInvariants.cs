using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Model;
using SilverAssertions;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The structural rules every parsed document must obey, checked in one place so both the unit
/// tests and the corpus gate can assert them. These are the invariants the emitter is entitled to
/// rely on: content sits where its placement says it may, the sectioning tree really is a tree,
/// and the document's lookup tables agree with the tree they were gathered from.
/// </summary>
internal static class DocumentInvariants
{
    /// <summary>Asserts every invariant against a parsed document.</summary>
    /// <param name="document">The document to check.</param>
    public static void AssertAll(TexinfoDocument document)
    {
        HashSet<TexinfoNode> reachable = new HashSet<TexinfoNode>(ReferenceEqualityComparer.Instance);
        foreach (TexinfoNode node in document.AllNodes())
        {
            reachable.Add(node);
        }

        AssertPlacements(document);
        AssertSectionTree(document);
        AssertAnchors(document, reachable);
        AssertIndexEntries(document, reachable);
        AssertFootnotes(document, reachable);
    }

    private static void AssertPlacements(TexinfoDocument document)
    {
        RequireBlocks(document.Preamble);
        RequireBlocks(document.Copying);
        foreach (SectionNode section in document.Sections)
        {
            CheckNode(section);
        }
        foreach (TexinfoNode node in document.Preamble)
        {
            CheckNode(node);
        }
        foreach (TexinfoNode node in document.Copying)
        {
            CheckNode(node);
        }
    }

    private static void CheckNode(TexinfoNode node)
    {
        switch (node)
        {
            case ParagraphNode paragraph:
                RequireInlines(paragraph.Content);
                paragraph.Content.Count.Should().BeGreaterThan(0);
                break;
            case HeadingNode heading:
                RequireInlines(heading.Content);
                break;
            case SectionNode section:
                RequireInlines(section.Title);
                RequireBlocks(section.Blocks);
                break;
            case BlockEnvironmentNode environment:
                RequireInlines(environment.Argument);
                RequireBlocks(environment.Blocks);
                break;
            case PreformattedNode preformatted:
                RequireInlines(preformatted.Content);
                break;
            case ListItemNode item:
                RequireBlocks(item.Blocks);
                break;
            case TableEntryNode entry:
                RequireBlocks(entry.Blocks);
                (entry.Terms.Count + entry.Blocks.Count).Should().BeGreaterThan(0);
                break;
            case TableTermNode term:
                RequireInlines(term.Content);
                break;
            case MultitableRowNode row:
                row.Cells.Count.Should().BeGreaterThan(0);
                break;
            case MultitableCellNode cell:
                RequireBlocks(cell.Blocks);
                break;
            case InlineCommandNode command:
                RequireInlines(command.Content);
                break;
            case CrossReferenceNode reference:
                RequireInlines(reference.Title);
                break;
            case LinkNode link:
                RequireInlines(link.Text);
                break;
            case FootnoteNode footnote:
                RequireInlines(footnote.Content);
                break;
            case IndexEntryNode indexEntry:
                RequireInlines(indexEntry.Content);
                break;
        }
        foreach (TexinfoNode child in node.ChildNodes)
        {
            CheckNode(child);
        }
    }

    private static void RequireInlines(IReadOnlyList<TexinfoNode> nodes)
    {
        foreach (TexinfoNode node in nodes)
        {
            node.Placement.HasFlag(TexinfoNodePlacement.Inline).Should().BeTrue();
        }
    }

    private static void RequireBlocks(IReadOnlyList<TexinfoNode> nodes)
    {
        foreach (TexinfoNode node in nodes)
        {
            node.Placement.HasFlag(TexinfoNodePlacement.Block).Should().BeTrue();
        }
    }

    private static void AssertSectionTree(TexinfoDocument document)
    {
        HashSet<SectionNode> seen = new HashSet<SectionNode>(ReferenceEqualityComparer.Instance);
        foreach (SectionNode section in document.Sections)
        {
            CheckSection(section, seen);
        }
    }

    private static void CheckSection(SectionNode section, HashSet<SectionNode> seen)
    {
        seen.Add(section).Should().BeTrue();
        foreach (SectionNode child in section.Children)
        {
            child.Level.Should().BeGreaterThan(section.Level);
            CheckSection(child, seen);
        }
    }

    private static void AssertAnchors(TexinfoDocument document, HashSet<TexinfoNode> reachable)
    {
        foreach (KeyValuePair<string, TexinfoAnchor> pair in document.Anchors)
        {
            pair.Value.Name.Should().Be(pair.Key);
            pair.Value.Name.Length.Should().BeGreaterThan(0);
            reachable.Contains(pair.Value.Target).Should().BeTrue();
        }
    }

    private static void AssertIndexEntries(TexinfoDocument document, HashSet<TexinfoNode> reachable)
    {
        int found = 0;
        foreach (TexinfoNode node in reachable)
        {
            if (node is IndexEntryNode)
            {
                found++;
            }
        }
        found.Should().Be(document.IndexEntries.Count);
        foreach (IndexEntryNode entry in document.IndexEntries)
        {
            reachable.Contains(entry).Should().BeTrue();
            entry.IndexName.Length.Should().Be(2);
        }
    }

    private static void AssertFootnotes(TexinfoDocument document, HashSet<TexinfoNode> reachable)
    {
        for (int i = 0; i < document.Footnotes.Count; i++)
        {
            document.Footnotes[i].Number.Should().Be(i + 1);
            reachable.Contains(document.Footnotes[i]).Should().BeTrue();
        }
    }
}
