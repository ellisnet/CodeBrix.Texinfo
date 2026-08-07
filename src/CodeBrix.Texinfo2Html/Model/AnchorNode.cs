using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A named destination from <c>@anchor{name}</c>. Anchors carry no visible content; they exist so
/// cross references can point at a spot that is not a node.
/// </summary>
internal sealed class AnchorNode : TexinfoNode
{
    /// <summary>Creates an anchor node.</summary>
    /// <param name="name">The anchor's name, as cross references will spell it.</param>
    /// <param name="position">Where the command started in the source.</param>
    public AnchorNode(string name, SourcePosition position) : base(position)
    {
        Name = name ?? string.Empty;
    }

    /// <summary>The anchor's name, as cross references will spell it.</summary>
    public string Name { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Both;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@anchor{{{Name}}}";
}
