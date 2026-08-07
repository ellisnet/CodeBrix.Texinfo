using System;
using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Model;

namespace CodeBrix.Texinfo2Html.Parsing;

/// <summary>
/// The parser's knowledge of what each built-in Texinfo command is: which ones open sections,
/// which wrap inline content, which stand for a fixed glyph, and which open block environments.
/// Commands that need bespoke handling - cross references, images, list environments and the
/// like - are absent here and dispatched by name in the parser itself.
/// </summary>
internal static class TexinfoCommandTable
{
    private static readonly Dictionary<string, (int Level, SectionKind Kind)> Sectioning =
        new Dictionary<string, (int, SectionKind)>(StringComparer.Ordinal)
        {
            { "top", (0, SectionKind.Top) },
            { "part", (0, SectionKind.Part) },
            { "chapter", (1, SectionKind.Numbered) },
            { "unnumbered", (1, SectionKind.Unnumbered) },
            { "appendix", (1, SectionKind.Appendix) },
            { "centerchap", (1, SectionKind.Unnumbered) },
            { "section", (2, SectionKind.Numbered) },
            { "unnumberedsec", (2, SectionKind.Unnumbered) },
            { "appendixsec", (2, SectionKind.Appendix) },
            { "appendixsection", (2, SectionKind.Appendix) },
            { "subsection", (3, SectionKind.Numbered) },
            { "unnumberedsubsec", (3, SectionKind.Unnumbered) },
            { "appendixsubsec", (3, SectionKind.Appendix) },
            { "subsubsection", (4, SectionKind.Numbered) },
            { "unnumberedsubsubsec", (4, SectionKind.Unnumbered) },
            { "appendixsubsubsec", (4, SectionKind.Appendix) }
        };

    private static readonly Dictionary<string, (HeadingKind Kind, int Level)> Headings =
        new Dictionary<string, (HeadingKind, int)>(StringComparer.Ordinal)
        {
            { "majorheading", (HeadingKind.Major, 1) },
            { "chapheading", (HeadingKind.Chapter, 1) },
            { "heading", (HeadingKind.Section, 2) },
            { "subheading", (HeadingKind.Subsection, 3) },
            { "subsubheading", (HeadingKind.Subsubsection, 4) },
            { "title", (HeadingKind.Title, 0) },
            { "subtitle", (HeadingKind.Subtitle, 0) },
            { "author", (HeadingKind.Author, 0) }
        };

    private static readonly Dictionary<string, string> IndexCommands =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "cindex", "cp" },
            { "findex", "fn" },
            { "vindex", "vr" },
            { "kindex", "ky" },
            { "pindex", "pg" },
            { "tindex", "tp" }
        };

    private static readonly Dictionary<string, InlineStyle> InlineStyles =
        new Dictionary<string, InlineStyle>(StringComparer.Ordinal)
        {
            { "code", InlineStyle.Code },
            { "emph", InlineStyle.Emphasis },
            { "strong", InlineStyle.Strong },
            { "b", InlineStyle.Bold },
            { "i", InlineStyle.Italic },
            { "t", InlineStyle.Typewriter },
            { "r", InlineStyle.Roman },
            { "sansserif", InlineStyle.SansSerif },
            { "slanted", InlineStyle.Slanted },
            { "sc", InlineStyle.SmallCaps },
            { "titlefont", InlineStyle.TitleFont },
            { "var", InlineStyle.Variable },
            { "file", InlineStyle.FileName },
            { "samp", InlineStyle.Sample },
            { "command", InlineStyle.CommandName },
            { "dfn", InlineStyle.Definition },
            { "option", InlineStyle.Option },
            { "env", InlineStyle.EnvironmentVariable },
            { "key", InlineStyle.Key },
            { "kbd", InlineStyle.Keyboard },
            { "cite", InlineStyle.Citation },
            { "asis", InlineStyle.AsIs },
            { "w", InlineStyle.NoBreak },
            { "math", InlineStyle.Math },
            { "sup", InlineStyle.Superscript },
            { "sub", InlineStyle.Subscript },
            { "dmn", InlineStyle.Dimension },
            { "indicateurl", InlineStyle.IndicateUrl },
            { "clicksequence", InlineStyle.ClickSequence },
            { "sortas", InlineStyle.SortAs },
            { "subentry", InlineStyle.SubEntry },
            { "seeentry", InlineStyle.SeeEntry },
            { "seealso", InlineStyle.SeeAlso }
        };

    private static readonly Dictionary<string, string> Glyphs =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "dots", "…" },
            { "enddots", "…." },
            { "bullet", "•" },
            { "minus", "−" },
            { "copyright", "©" },
            { "registeredsymbol", "®" },
            { "result", "⇒" },
            { "expansion", "↦" },
            { "arrow", "→" },
            { "click", "→" },
            { "print", "⊣" },
            { "error", "error→" },
            { "equiv", "≡" },
            { "point", "∗" },
            { "euro", "€" },
            { "pounds", "£" },
            { "textdegree", "°" },
            { "leq", "≤" },
            { "geq", "≥" },
            { "LaTeX", "LaTeX" },
            { "TeX", "TeX" },
            { "comma", "," },
            { "tie", " " },
            { "quoteleft", "‘" },
            { "quoteright", "’" },
            { "quotedblleft", "“" },
            { "quotedblright", "”" },
            { "quotesinglbase", "‚" },
            { "quotedblbase", "„" },
            { "guillemetleft", "«" },
            { "guillemetright", "»" },
            { "guillemotleft", "«" },
            { "guillemotright", "»" },
            { "exclamdown", "¡" },
            { "questiondown", "¿" },
            { "aa", "å" },
            { "AA", "Å" },
            { "ae", "æ" },
            { "AE", "Æ" },
            { "o", "ø" },
            { "O", "Ø" },
            { "oe", "œ" },
            { "OE", "Œ" },
            { "ss", "ß" },
            { "l", "ł" },
            { "L", "Ł" },
            { "ordf", "ª" },
            { "ordm", "º" }
        };

    private static readonly Dictionary<string, (TexinfoBlockKind Kind, bool Preformatted)> BlockEnvironments =
        new Dictionary<string, (TexinfoBlockKind, bool)>(StringComparer.Ordinal)
        {
            { "example", (TexinfoBlockKind.Example, true) },
            { "smallexample", (TexinfoBlockKind.SmallExample, true) },
            { "lisp", (TexinfoBlockKind.Lisp, true) },
            { "smalllisp", (TexinfoBlockKind.SmallLisp, true) },
            { "display", (TexinfoBlockKind.Display, true) },
            { "smalldisplay", (TexinfoBlockKind.SmallDisplay, true) },
            { "format", (TexinfoBlockKind.Format, true) },
            { "smallformat", (TexinfoBlockKind.SmallFormat, true) },
            { "quotation", (TexinfoBlockKind.Quotation, false) },
            { "smallquotation", (TexinfoBlockKind.SmallQuotation, false) },
            { "indentedblock", (TexinfoBlockKind.IndentedBlock, false) },
            { "smallindentedblock", (TexinfoBlockKind.SmallIndentedBlock, false) },
            { "cartouche", (TexinfoBlockKind.Cartouche, false) },
            { "group", (TexinfoBlockKind.Group, false) },
            { "raggedright", (TexinfoBlockKind.RaggedRight, false) },
            { "flushleft", (TexinfoBlockKind.FlushLeft, false) },
            { "flushright", (TexinfoBlockKind.FlushRight, false) },
            { "titlepage", (TexinfoBlockKind.TitlePage, false) },
            { "documentdescription", (TexinfoBlockKind.DocumentDescription, false) }
        };

    /// <summary>
    /// Block environments that are recognized as blocks - so their <c>@end</c> lines up and their
    /// content survives - but whose particular meaning is not implemented yet. Each occurrence
    /// warns once and renders as a plain block.
    /// </summary>
    private static readonly HashSet<string> GenericBlockEnvironments =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "float", "deffn", "defun", "defmac", "defspec", "defvr", "defvar", "defopt",
            "deftypefn", "deftypefun", "deftypevr", "deftypevar", "deftp", "defcv", "defivar",
            "defop", "defmethod", "deftypecv", "deftypeivar", "deftypeop", "deftypemethod"
        };

    /// <summary>
    /// Commands whose whole line is a setting to be recorded and otherwise ignored. Their
    /// arguments are never parsed as Texinfo, which keeps stray <c>@</c> sequences inside things
    /// like <c>@everyheading</c> from being mistaken for commands.
    /// </summary>
    private static readonly HashSet<string> RecordedSettings =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "setfilename", "novalidate", "setchapternewpage", "paragraphindent",
            "firstparagraphindent", "exampleindent", "headings", "everyheading", "evenheading",
            "oddheading", "everyfooting", "evenfooting", "oddfooting", "finalout",
            "allowcodebreaks", "setcodequotes", "codequoteundirected", "codequotebacktick",
            "frenchspacing", "kbdinputstyle", "footnotestyle", "urefbreakstyle",
            "xrefautomaticsectiontitle", "fonttextsize", "deftypefnnewline", "microtype",
            "afourpaper", "afivepaper", "afourlatex", "afourwide", "smallbook", "pagesizes",
            "raisesections", "lowersections", "definfoenclose", "defindex", "defcodeindex",
            "shorttitlepage", "clickstyle", "setcontentsaftertitlepage",
            "setshortcontentsaftertitlepage"
        };

    /// <summary>Brace commands whose content is deliberately dropped, such as <c>@hyphenation</c>.</summary>
    private static readonly HashSet<string> DiscardedBraceCommands =
        new HashSet<string>(StringComparer.Ordinal) { "hyphenation" };

    /// <summary>True when the command opens a sectioning unit.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    public static bool IsSectioning(string name) => Sectioning.ContainsKey(name);

    /// <summary>Looks up the level and numbering style of a sectioning command.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="level">Receives the nesting level.</param>
    /// <param name="kind">Receives the numbering style.</param>
    public static bool TryGetSectioning(string name, out int level, out SectionKind kind)
    {
        if (Sectioning.TryGetValue(name, out (int Level, SectionKind Kind) found))
        {
            level = found.Level;
            kind = found.Kind;
            return true;
        }
        level = 0;
        kind = SectionKind.Numbered;
        return false;
    }

    /// <summary>Looks up a standalone heading command.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="kind">Receives which kind of heading it is.</param>
    /// <param name="level">Receives the heading's size level.</param>
    public static bool TryGetHeading(string name, out HeadingKind kind, out int level)
    {
        if (Headings.TryGetValue(name, out (HeadingKind Kind, int Level) found))
        {
            kind = found.Kind;
            level = found.Level;
            return true;
        }
        kind = HeadingKind.Section;
        level = 2;
        return false;
    }

    /// <summary>Looks up the two-letter index an index command files entries in.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="indexName">Receives the index name, such as <c>cp</c>.</param>
    public static bool TryGetIndexName(string name, out string indexName)
        => IndexCommands.TryGetValue(name, out indexName);

    /// <summary>Looks up the semantic role of an inline command that wraps content.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="style">Receives the semantic role.</param>
    public static bool TryGetInlineStyle(string name, out InlineStyle style)
        => InlineStyles.TryGetValue(name, out style);

    /// <summary>Looks up the text a no-argument glyph command stands for.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="text">Receives the text.</param>
    public static bool TryGetGlyph(string name, out string text) => Glyphs.TryGetValue(name, out text);

    /// <summary>Looks up a block environment.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="kind">Receives which environment it is.</param>
    /// <param name="isPreformatted">Receives whether its body is preformatted text.</param>
    public static bool TryGetBlockEnvironment(string name, out TexinfoBlockKind kind, out bool isPreformatted)
    {
        if (BlockEnvironments.TryGetValue(name, out (TexinfoBlockKind Kind, bool Preformatted) found))
        {
            kind = found.Kind;
            isPreformatted = found.Preformatted;
            return true;
        }
        kind = TexinfoBlockKind.Unknown;
        isPreformatted = false;
        return false;
    }

    /// <summary>True for block environments that are parsed as blocks but not yet implemented.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    public static bool IsGenericBlockEnvironment(string name) => GenericBlockEnvironments.Contains(name);

    /// <summary>True when the command's whole line is a setting to record and otherwise ignore.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    public static bool IsRecordedSetting(string name) => RecordedSettings.Contains(name);

    /// <summary>True when the command takes a brace argument that is deliberately dropped.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    public static bool IsDiscardedBraceCommand(string name) => DiscardedBraceCommands.Contains(name);
}
