using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A standalone heading from the <c>@heading</c> family, or a title-page line from <c>@title</c>,
/// <c>@subtitle</c> or <c>@author</c>. None of these create structure: they print a heading and
/// nothing more, which is why they are ordinary blocks rather than <see cref="SectionNode"/>s.
/// </summary>
internal sealed class HeadingNode : TexinfoNode
{
    /// <summary>Creates a heading.</summary>
    /// <param name="commandName">The command that produced the heading, without <c>@</c>.</param>
    /// <param name="kind">Which kind of heading it is.</param>
    /// <param name="level">The size level, matching <see cref="SectionNode.Level"/>.</param>
    /// <param name="content">The heading text.</param>
    /// <param name="position">Where the command started in the source.</param>
    public HeadingNode(string commandName, HeadingKind kind, int level,
        IReadOnlyList<TexinfoNode> content, SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Kind = kind;
        Level = level;
        Content = content ?? new List<TexinfoNode>();
    }

    /// <summary>The command that produced the heading, without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>Which kind of heading it is.</summary>
    public HeadingKind Kind { get; }

    /// <summary>
    /// The size level, using the same scale as <see cref="SectionNode.Level"/> so a heading and a
    /// real section of the same size render alike. Title-page lines use level 0.
    /// </summary>
    public int Level { get; }

    /// <summary>The heading text.</summary>
    public IReadOnlyList<TexinfoNode> Content { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Content;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} ({Kind})";
}
