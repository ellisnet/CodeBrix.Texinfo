namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// Which cross-reference command produced a <see cref="CrossReferenceNode"/>. The three forms
/// differ only in the wording that surrounds the link in print output.
/// </summary>
internal enum CrossReferenceKind
{
    /// <summary><c>@ref</c> - a bare reference with no introductory wording.</summary>
    Reference,

    /// <summary><c>@xref</c> - a reference that starts a sentence ("See ...").</summary>
    SentenceStart,

    /// <summary><c>@pxref</c> - a parenthetical reference ("see ...").</summary>
    Parenthetical,

    /// <summary><c>@inforef</c> - a reference into an Info-only manual.</summary>
    InfoReference
}
