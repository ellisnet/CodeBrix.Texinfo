using System.Collections.Generic;
using System.Text;
using CodeBrix.Texinfo2Html.Model;

namespace CodeBrix.Texinfo2Html.Parsing;

/// <summary>
/// Helpers for working with the flat inline node lists the parser produces: flattening them to
/// plain text, splitting a command's argument list on its commas, and trimming the whitespace
/// that line and brace arguments pick up at their edges.
/// </summary>
internal static class InlineNodes
{
    private static readonly List<TexinfoNode> Empty = new List<TexinfoNode>();

    /// <summary>An empty node list, shared by callers that need one.</summary>
    public static IReadOnlyList<TexinfoNode> None => Empty;

    /// <summary>
    /// Flattens nodes to the plain text they stand for. Used wherever Texinfo needs a string
    /// rather than markup - node names, file names, index sort keys.
    /// </summary>
    /// <param name="nodes">The nodes to flatten.</param>
    public static string ToPlainText(IReadOnlyList<TexinfoNode> nodes)
    {
        StringBuilder builder = new StringBuilder();
        Append(builder, nodes);
        return builder.ToString();
    }

    /// <summary>
    /// Flattens nodes to the name they stand for: their plain text with every run of whitespace
    /// collapsed to a single space. Texinfo names are written inside braces and may be broken
    /// across lines wherever the paragraph needs it, so the same destination is routinely spelled
    /// with a newline in the reference and a space in the definition.
    /// </summary>
    /// <param name="nodes">The nodes to flatten.</param>
    public static string ToName(IReadOnlyList<TexinfoNode> nodes)
    {
        string text = ToPlainText(nodes);
        StringBuilder builder = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }

    /// <summary>Returns one part of a split argument as a name, flattened and whitespace-collapsed.</summary>
    /// <param name="parts">The parts returned by <see cref="SplitOnCommas"/>.</param>
    /// <param name="index">Which part to read.</param>
    public static string PartName(List<List<TexinfoNode>> parts, int index)
        => index < parts.Count ? ToName(parts[index]) : string.Empty;

    private static void Append(StringBuilder builder, IEnumerable<TexinfoNode> nodes)
    {
        foreach (TexinfoNode node in nodes)
        {
            switch (node)
            {
                case TextNode text:
                    builder.Append(text.Text);
                    break;
                case GlyphNode glyph:
                    builder.Append(glyph.Text);
                    break;
                case VerbatimNode verbatim:
                    builder.Append(verbatim.Text);
                    break;
                case LineBreakNode:
                    builder.Append(' ');
                    break;
                case MusicSnippetNode:
                case AnchorNode:
                case IndexEntryNode:
                case FootnoteNode:
                    break;
                case CrossReferenceNode reference:
                    if (reference.Title.Count > 0)
                    {
                        Append(builder, reference.Title);
                    }
                    else
                    {
                        builder.Append(reference.NodeName);
                    }
                    break;
                case LinkNode link:
                    if (link.Text.Count > 0)
                    {
                        Append(builder, link.Text);
                    }
                    else
                    {
                        builder.Append(link.Target);
                    }
                    break;
                default:
                    Append(builder, node.ChildNodes);
                    break;
            }
        }
    }

    /// <summary>
    /// Splits a command's parsed argument into its comma-separated parts. Only commas in
    /// top-level text split; a comma written as <c>@comma{}</c> or one inside a nested command's
    /// argument is protected, which is exactly what Texinfo promises.
    /// </summary>
    /// <param name="nodes">The parsed argument.</param>
    /// <param name="maximumParts">The most parts to produce; further commas stay as text.</param>
    public static List<List<TexinfoNode>> SplitOnCommas(IReadOnlyList<TexinfoNode> nodes, int maximumParts)
    {
        List<List<TexinfoNode>> parts = new List<List<TexinfoNode>>();
        List<TexinfoNode> current = new List<TexinfoNode>();
        parts.Add(current);
        foreach (TexinfoNode node in nodes)
        {
            if (!(node is TextNode text) || text.Text.IndexOf(',') < 0)
            {
                current.Add(node);
                continue;
            }
            int start = 0;
            for (int i = 0; i < text.Text.Length; i++)
            {
                if (text.Text[i] != ',' || (maximumParts > 0 && parts.Count >= maximumParts))
                {
                    continue;
                }
                if (i > start)
                {
                    current.Add(new TextNode(text.Text.Substring(start, i - start), text.Position));
                }
                current = new List<TexinfoNode>();
                parts.Add(current);
                start = i + 1;
            }
            if (start < text.Text.Length)
            {
                current.Add(new TextNode(text.Text.Substring(start), text.Position));
            }
        }
        return parts;
    }

    /// <summary>Returns the plain text of one part of a split argument, trimmed.</summary>
    /// <param name="parts">The parts returned by <see cref="SplitOnCommas"/>.</param>
    /// <param name="index">Which part to read.</param>
    public static string PartText(List<List<TexinfoNode>> parts, int index)
        => index < parts.Count ? ToPlainText(parts[index]).Trim() : string.Empty;

    /// <summary>Returns one part of a split argument with its edge whitespace removed.</summary>
    /// <param name="parts">The parts returned by <see cref="SplitOnCommas"/>.</param>
    /// <param name="index">Which part to read.</param>
    public static List<TexinfoNode> Part(List<List<TexinfoNode>> parts, int index)
        => index < parts.Count ? Trim(parts[index]) : new List<TexinfoNode>();

    /// <summary>
    /// Removes whitespace at both ends of a node list. Paragraph and argument content is trimmed;
    /// preformatted content never is, because there the whitespace is the point.
    /// </summary>
    /// <param name="nodes">The nodes to trim.</param>
    public static List<TexinfoNode> Trim(IReadOnlyList<TexinfoNode> nodes)
    {
        int first = 0;
        int last = nodes.Count - 1;
        while (first <= last && IsBlankText(nodes[first]))
        {
            first++;
        }
        while (last >= first && IsBlankText(nodes[last]))
        {
            last--;
        }
        List<TexinfoNode> result = new List<TexinfoNode>();
        for (int i = first; i <= last; i++)
        {
            result.Add(nodes[i]);
        }
        if (result.Count > 0 && result[0] is TextNode start)
        {
            result[0] = new TextNode(start.Text.TrimStart(), start.Position);
        }
        if (result.Count > 0 && result[result.Count - 1] is TextNode end)
        {
            result[result.Count - 1] = new TextNode(end.Text.TrimEnd(), end.Position);
        }
        return result;
    }

    /// <summary>
    /// True when the nodes amount to more than whitespace, so a paragraph built from them is
    /// worth emitting.
    /// </summary>
    /// <param name="nodes">The nodes to test.</param>
    public static bool HasVisibleContent(IReadOnlyList<TexinfoNode> nodes)
    {
        foreach (TexinfoNode node in nodes)
        {
            if (!IsBlankText(node))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsBlankText(TexinfoNode node)
        => node is TextNode text && text.Text.Trim().Length == 0;
}
