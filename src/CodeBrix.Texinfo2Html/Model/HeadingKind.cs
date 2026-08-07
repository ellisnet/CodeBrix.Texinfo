namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// Which kind of standalone heading a <see cref="HeadingNode"/> represents. These headings are
/// deliberately not sectioning units: Texinfo's <c>@heading</c> family prints a heading without
/// creating structure, without numbering it and without listing it in the table of contents.
/// </summary>
internal enum HeadingKind
{
    /// <summary><c>@majorheading</c> - chapter-sized, with extra space above.</summary>
    Major,

    /// <summary><c>@chapheading</c> - chapter-sized.</summary>
    Chapter,

    /// <summary><c>@heading</c> - section-sized.</summary>
    Section,

    /// <summary><c>@subheading</c> - subsection-sized.</summary>
    Subsection,

    /// <summary><c>@subsubheading</c> - subsubsection-sized.</summary>
    Subsubsection,

    /// <summary><c>@title</c> - the document title on a title page.</summary>
    Title,

    /// <summary><c>@subtitle</c> - the document subtitle on a title page.</summary>
    Subtitle,

    /// <summary><c>@author</c> - an author credit on a title page.</summary>
    Author
}
