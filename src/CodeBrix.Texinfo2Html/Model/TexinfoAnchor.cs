using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// One entry of a document's table of named destinations: everything a cross reference could be
/// pointing at, whether it came from <c>@node</c>, <c>@anchor</c> or a numbered float.
/// </summary>
internal sealed class TexinfoAnchor
{
    /// <summary>Creates a table entry for a named destination.</summary>
    /// <param name="name">The destination's name exactly as the document spelled it.</param>
    /// <param name="kind">Which command created it.</param>
    /// <param name="target">The node that carries the destination.</param>
    /// <param name="position">Where the command appeared in the source.</param>
    public TexinfoAnchor(string name, TexinfoAnchorKind kind, TexinfoNode target, SourcePosition position)
    {
        Name = name ?? string.Empty;
        Kind = kind;
        Target = target;
        Position = position;
    }

    /// <summary>The destination's name exactly as the document spelled it.</summary>
    public string Name { get; }

    /// <summary>Which command created the destination.</summary>
    public TexinfoAnchorKind Kind { get; }

    /// <summary>
    /// The node that carries the destination - a <see cref="SectionNode"/> for a node that
    /// introduced a sectioning command, otherwise the anchor or node marker itself.
    /// </summary>
    public TexinfoNode Target { get; internal set; }

    /// <summary>Where the command appeared in the source.</summary>
    public SourcePosition Position { get; }

    /// <summary>
    /// The identifier the emitter assigned for linking, filled in by a later pass. The parser
    /// leaves this empty.
    /// </summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>Formats the anchor for diagnostics.</summary>
    public override string ToString() => $"{Kind} '{Name}'";
}
