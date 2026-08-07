using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A cross reference from <c>@ref</c>, <c>@xref</c>, <c>@pxref</c> or <c>@inforef</c>. Texinfo
/// gives these commands up to five arguments - node name, cross-reference name, title, Info file
/// and printed manual - and every one of them is optional except the node name. Resolving the
/// reference to a target is a later pass; the parser only records what the document said.
/// </summary>
internal sealed class CrossReferenceNode : TexinfoNode
{
    /// <summary>Creates a cross-reference node.</summary>
    /// <param name="kind">Which of the reference commands produced this node.</param>
    /// <param name="nodeName">The referenced node's name (the first argument).</param>
    /// <param name="label">The cross-reference name, used for Info-style references.</param>
    /// <param name="title">The text to display in place of the node name, if given.</param>
    /// <param name="infoFile">The external manual's Info file name, if given.</param>
    /// <param name="manual">The external manual's printed title, if given.</param>
    /// <param name="position">Where the command started in the source.</param>
    public CrossReferenceNode(CrossReferenceKind kind, string nodeName, string label,
        IReadOnlyList<TexinfoNode> title, string infoFile, string manual, SourcePosition position)
        : base(position)
    {
        Kind = kind;
        NodeName = nodeName ?? string.Empty;
        Label = label ?? string.Empty;
        Title = title ?? new List<TexinfoNode>();
        InfoFile = infoFile ?? string.Empty;
        Manual = manual ?? string.Empty;
    }

    /// <summary>Which of the reference commands produced this node.</summary>
    public CrossReferenceKind Kind { get; }

    /// <summary>The referenced node's name.</summary>
    public string NodeName { get; }

    /// <summary>The cross-reference name argument, usually empty in print-shaped documents.</summary>
    public string Label { get; }

    /// <summary>The text to display in place of the node name; empty when not given.</summary>
    public IReadOnlyList<TexinfoNode> Title { get; }

    /// <summary>The external manual's Info file name; empty for a reference within this document.</summary>
    public string InfoFile { get; }

    /// <summary>The external manual's printed title; empty for a reference within this document.</summary>
    public string Manual { get; }

    /// <summary>True when the reference points into another manual rather than this document.</summary>
    public bool IsExternal => InfoFile.Length > 0 || Manual.Length > 0;

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Title;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"{Kind} to '{NodeName}'";
}
