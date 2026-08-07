using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// One sectioning unit - a chapter, section, subsection and so on - holding the content written
/// directly under its heading plus the units nested inside it. Unlike the other nodes, a section
/// is filled in as parsing proceeds, so its two child collections are mutable lists that the
/// parser appends to and that every later pass treats as read-only.
/// </summary>
internal sealed class SectionNode : TexinfoNode
{
    /// <summary>Creates a sectioning unit.</summary>
    /// <param name="commandName">The sectioning command without <c>@</c>.</param>
    /// <param name="level">The nesting level: 0 for <c>@top</c> and <c>@part</c>, 1 for chapters.</param>
    /// <param name="kind">How the unit is numbered.</param>
    /// <param name="title">The heading text.</param>
    /// <param name="nodeName">The name of the <c>@node</c> that introduced it, or an empty string.</param>
    /// <param name="position">Where the sectioning command started in the source.</param>
    public SectionNode(string commandName, int level, SectionKind kind, IReadOnlyList<TexinfoNode> title,
        string nodeName, SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Level = level;
        Kind = kind;
        Title = title ?? new List<TexinfoNode>();
        NodeName = nodeName ?? string.Empty;
    }

    /// <summary>The sectioning command that produced the unit, without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>
    /// The nesting level: 0 for <c>@top</c> and <c>@part</c>, 1 for chapter-level commands, 2 for
    /// section-level, 3 for subsection-level and 4 for subsubsection-level.
    /// </summary>
    public int Level { get; }

    /// <summary>How the unit is numbered.</summary>
    public SectionKind Kind { get; }

    /// <summary>The heading text.</summary>
    public IReadOnlyList<TexinfoNode> Title { get; }

    /// <summary>
    /// The name of the <c>@node</c> that introduced this unit, or an empty string when it was
    /// written without one. Cross references target this name.
    /// </summary>
    public string NodeName { get; }

    /// <summary>The content written directly under this heading, before any nested unit.</summary>
    public List<TexinfoNode> Blocks { get; } = new List<TexinfoNode>();

    /// <summary>The sectioning units nested inside this one.</summary>
    public List<SectionNode> Children { get; } = new List<SectionNode>();

    /// <summary>
    /// The section number assigned by the numbering pass, such as <c>2.3.1</c> or <c>A.1</c>, or
    /// an empty string for unnumbered units. The parser leaves this empty.
    /// </summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// The unique HTML identifier assigned by the semantic pass, used as the unit's anchor and as
    /// the target of every cross reference that names it. The parser leaves this empty.
    /// </summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>
    /// The heading rank assigned by the semantic pass, from 1 to 6, matching the <c>h1</c> to
    /// <c>h6</c> element the emitter uses. It follows the unit's depth in the sectioning tree
    /// rather than <see cref="Level"/>, so a document that skips a level still nests correctly in
    /// the generated PDF outline. The parser leaves this at zero.
    /// </summary>
    public int HeadingLevel { get; set; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (TexinfoNode node in Title)
            {
                yield return node;
            }
            foreach (TexinfoNode node in Blocks)
            {
                yield return node;
            }
            foreach (SectionNode child in Children)
            {
                yield return child;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} (level {Level}) '{NodeName}'";
}
