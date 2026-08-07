namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// Which standalone instruction a <see cref="DirectiveNode"/> carries. These are the commands
/// that produce no content of their own but tell the emitter to do something at that point in
/// the document.
/// </summary>
internal enum DirectiveKind
{
    /// <summary><c>@contents</c> - print the table of contents here.</summary>
    Contents,

    /// <summary><c>@shortcontents</c> or <c>@summarycontents</c> - print a chapter-only table of contents here.</summary>
    ShortContents,

    /// <summary><c>@insertcopying</c> - print the <c>@copying</c> text here.</summary>
    InsertCopying,

    /// <summary><c>@printindex</c> - print the named index here.</summary>
    PrintIndex,

    /// <summary><c>@page</c> - start a new page.</summary>
    PageBreak,

    /// <summary><c>@sp</c> or <c>@vskip</c> - leave vertical space.</summary>
    VerticalSpace,

    /// <summary><c>@need</c> - start a new page unless the given space remains.</summary>
    NeedSpace
}
