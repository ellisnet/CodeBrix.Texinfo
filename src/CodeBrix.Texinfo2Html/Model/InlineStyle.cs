namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// The semantic role of an <see cref="InlineCommandNode"/>. Texinfo distinguishes many inline
/// commands that a browser would render identically; the distinctions are kept here so the
/// emitter decides the styling in one place instead of scattering command names through it.
/// </summary>
internal enum InlineStyle
{
    /// <summary>A command the parser recognized but has no dedicated styling for.</summary>
    Generic,

    /// <summary><c>@code</c> - a fragment of code or a literal value.</summary>
    Code,

    /// <summary><c>@emph</c> - emphasis.</summary>
    Emphasis,

    /// <summary><c>@strong</c> - strong emphasis.</summary>
    Strong,

    /// <summary><c>@b</c> - bold face, chosen for its appearance rather than its meaning.</summary>
    Bold,

    /// <summary><c>@i</c> - italic face, chosen for its appearance rather than its meaning.</summary>
    Italic,

    /// <summary><c>@t</c> - fixed-width face, chosen for its appearance rather than its meaning.</summary>
    Typewriter,

    /// <summary><c>@r</c> - roman face, used to break out of a surrounding fixed-width context.</summary>
    Roman,

    /// <summary><c>@sansserif</c> - sans-serif face.</summary>
    SansSerif,

    /// <summary><c>@slanted</c> - slanted face.</summary>
    Slanted,

    /// <summary><c>@sc</c> - small capitals.</summary>
    SmallCaps,

    /// <summary><c>@titlefont</c> - the title face used on a title page.</summary>
    TitleFont,

    /// <summary><c>@var</c> - a metasyntactic variable, standing for something to substitute.</summary>
    Variable,

    /// <summary><c>@file</c> - a file name.</summary>
    FileName,

    /// <summary><c>@samp</c> - a literal sequence of characters, quoted when rendered.</summary>
    Sample,

    /// <summary><c>@command</c> - the name of a command-line program.</summary>
    CommandName,

    /// <summary><c>@dfn</c> - the defining occurrence of a term.</summary>
    Definition,

    /// <summary><c>@option</c> - a command-line option.</summary>
    Option,

    /// <summary><c>@env</c> - an environment variable.</summary>
    EnvironmentVariable,

    /// <summary><c>@key</c> - the name of a key on a keyboard.</summary>
    Key,

    /// <summary><c>@kbd</c> - characters typed by the user.</summary>
    Keyboard,

    /// <summary><c>@cite</c> - the title of a cited work.</summary>
    Citation,

    /// <summary><c>@asis</c> - content to be rendered with no styling at all.</summary>
    AsIs,

    /// <summary><c>@w</c> - content that must not be broken across lines.</summary>
    NoBreak,

    /// <summary><c>@math</c> - a mathematical expression; degraded to styled text.</summary>
    Math,

    /// <summary><c>@sup</c> - superscript.</summary>
    Superscript,

    /// <summary><c>@sub</c> - subscript.</summary>
    Subscript,

    /// <summary><c>@dmn</c> - a unit of measure following a number.</summary>
    Dimension,

    /// <summary><c>@indicateurl</c> - a URL that is shown but not made into a link.</summary>
    IndicateUrl,

    /// <summary><c>@clicksequence</c> - a sequence of user interface actions.</summary>
    ClickSequence,

    /// <summary><c>@sortas</c> - a sort key attached to the index entry that contains it.</summary>
    SortAs,

    /// <summary><c>@subentry</c> - a second-level index entry.</summary>
    SubEntry,

    /// <summary><c>@seeentry</c> - an index entry redirecting to another entry.</summary>
    SeeEntry,

    /// <summary><c>@seealso</c> - an index entry pointing at a related entry.</summary>
    SeeAlso
}
