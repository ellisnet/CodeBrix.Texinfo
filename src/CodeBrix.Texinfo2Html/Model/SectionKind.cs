namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// How a sectioning unit is numbered. Texinfo spells the same four heading levels three times
/// over - numbered, unnumbered and appendix - and adds <c>@top</c> and <c>@part</c> above them.
/// </summary>
internal enum SectionKind
{
    /// <summary><c>@top</c> - the document's topmost unit, above every chapter.</summary>
    Top,

    /// <summary><c>@part</c> - a group of chapters, unnumbered and without a table-of-contents number.</summary>
    Part,

    /// <summary><c>@chapter</c>, <c>@section</c>, ... - numbered with arabic numerals.</summary>
    Numbered,

    /// <summary><c>@unnumbered</c>, <c>@unnumberedsec</c>, ... - present in the contents but unnumbered.</summary>
    Unnumbered,

    /// <summary><c>@appendix</c>, <c>@appendixsec</c>, ... - numbered with a leading letter.</summary>
    Appendix
}
