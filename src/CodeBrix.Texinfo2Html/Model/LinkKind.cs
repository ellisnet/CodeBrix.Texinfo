namespace CodeBrix.Texinfo2Html.Model;

/// <summary>What a <see cref="LinkNode"/> points at.</summary>
internal enum LinkKind
{
    /// <summary><c>@url</c> or <c>@uref</c> - a web address.</summary>
    Url,

    /// <summary><c>@email</c> - an electronic mail address.</summary>
    Email
}
