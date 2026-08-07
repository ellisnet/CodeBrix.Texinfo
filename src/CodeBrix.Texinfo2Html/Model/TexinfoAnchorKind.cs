namespace CodeBrix.Texinfo2Html.Model;

/// <summary>What kind of command created a named destination.</summary>
internal enum TexinfoAnchorKind
{
    /// <summary><c>@node</c> - a structural node, usually introducing a sectioning command.</summary>
    Node,

    /// <summary><c>@anchor</c> - a named spot inside a section.</summary>
    Anchor,

    /// <summary><c>@float</c> - a numbered float that cross references can target.</summary>
    Float
}
