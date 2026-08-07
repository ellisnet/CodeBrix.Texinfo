namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// Which block environment a <see cref="BlockEnvironmentNode"/> or <see cref="PreformattedNode"/>
/// represents. The <c>small</c> variants differ from their plain counterparts only in font size,
/// but they are kept apart so the emitter, not the parser, decides what "small" means.
/// </summary>
internal enum TexinfoBlockKind
{
    /// <summary>A block environment the parser did not recognize; its content is kept as-is.</summary>
    Unknown,

    /// <summary><c>@quotation</c> - an indented quotation, optionally labelled.</summary>
    Quotation,

    /// <summary><c>@smallquotation</c> - a quotation set in a smaller font.</summary>
    SmallQuotation,

    /// <summary><c>@indentedblock</c> - indented text with no other change.</summary>
    IndentedBlock,

    /// <summary><c>@smallindentedblock</c> - indented text in a smaller font.</summary>
    SmallIndentedBlock,

    /// <summary><c>@cartouche</c> - content inside a printed box.</summary>
    Cartouche,

    /// <summary><c>@group</c> - content that should not be split across pages.</summary>
    Group,

    /// <summary><c>@raggedright</c> - text set without justification.</summary>
    RaggedRight,

    /// <summary><c>@flushleft</c> - lines aligned on the left with no filling.</summary>
    FlushLeft,

    /// <summary><c>@flushright</c> - lines aligned on the right with no filling.</summary>
    FlushRight,

    /// <summary><c>@titlepage</c> - the document's title page.</summary>
    TitlePage,

    /// <summary><c>@copying</c> - the copyright and licence notice, printed where <c>@insertcopying</c> appears.</summary>
    Copying,

    /// <summary><c>@documentdescription</c> - a description recorded for output metadata.</summary>
    DocumentDescription,

    /// <summary><c>@example</c> - preformatted text in a fixed-width font.</summary>
    Example,

    /// <summary><c>@smallexample</c> - an example in a smaller font.</summary>
    SmallExample,

    /// <summary><c>@lisp</c> - preformatted Lisp source.</summary>
    Lisp,

    /// <summary><c>@smalllisp</c> - Lisp source in a smaller font.</summary>
    SmallLisp,

    /// <summary><c>@display</c> - preformatted text in the body font.</summary>
    Display,

    /// <summary><c>@smalldisplay</c> - display text in a smaller font.</summary>
    SmallDisplay,

    /// <summary><c>@format</c> - preformatted text with no indentation.</summary>
    Format,

    /// <summary><c>@smallformat</c> - format text in a smaller font.</summary>
    SmallFormat
}
