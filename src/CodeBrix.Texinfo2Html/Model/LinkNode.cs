using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// An external link from <c>@url</c>, <c>@uref</c> or <c>@email</c>. All three commands take the
/// target first and an optional display text second; <c>@uref</c> additionally takes a third
/// argument used when the output format cannot show links at all.
/// </summary>
internal sealed class LinkNode : TexinfoNode
{
    /// <summary>Creates a link node.</summary>
    /// <param name="kind">Whether the link is a web address or a mail address.</param>
    /// <param name="target">The address itself.</param>
    /// <param name="text">The text to display; empty means display the address.</param>
    /// <param name="replacement">Text replacing the whole link in link-less output formats.</param>
    /// <param name="position">Where the command started in the source.</param>
    public LinkNode(LinkKind kind, string target, IReadOnlyList<TexinfoNode> text, string replacement,
        SourcePosition position) : base(position)
    {
        Kind = kind;
        Target = target ?? string.Empty;
        Text = text ?? new List<TexinfoNode>();
        Replacement = replacement ?? string.Empty;
    }

    /// <summary>Whether the link is a web address or a mail address.</summary>
    public LinkKind Kind { get; }

    /// <summary>The address itself.</summary>
    public string Target { get; }

    /// <summary>The text to display; empty means the address is shown.</summary>
    public IReadOnlyList<TexinfoNode> Text { get; }

    /// <summary>Text replacing the whole link in output formats without links; usually empty.</summary>
    public string Replacement { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes => Text;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"{Kind} '{Target}'";
}
