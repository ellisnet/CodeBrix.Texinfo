using System;
using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html.Preprocessing;

/// <summary>
/// The names of genuine GNU Texinfo commands, compiled from the GNU Texinfo 7.2 manual's
/// command list. Used to reject <c>@macro</c>/<c>@alias</c> definitions that would shadow a
/// built-in command: the built-in keeps working and the definition is skipped with a warning
/// (the definition is almost always aimed at a different output engine's implementation).
/// Deliberately absent: names that documents commonly define as macros and that Texinfo itself
/// does not own (for example LilyPond's <c>@subsubsubheading</c>, <c>@iref</c> and
/// <c>@version</c>).
/// </summary>
internal static class KnownTexinfoCommands
{
    private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        // Sectioning and structure
        "node", "top", "part", "chapter", "section", "subsection", "subsubsection",
        "appendix", "appendixsec", "appendixsection", "appendixsubsec", "appendixsubsubsec",
        "unnumbered", "unnumberedsec", "unnumberedsubsec", "unnumberedsubsubsec",
        "chapheading", "majorheading", "heading", "subheading", "subsubheading", "centerchap",
        // Block environments
        "example", "smallexample", "lisp", "smalllisp", "verbatim", "display", "smalldisplay",
        "format", "smallformat", "quotation", "smallquotation", "indentedblock",
        "smallindentedblock", "cartouche", "itemize", "enumerate", "table", "ftable", "vtable",
        "multitable", "headitem", "item", "itemx", "tab", "columnfractions", "group",
        "raggedright", "flushleft", "flushright", "exdent", "menu", "detailmenu", "direntry",
        "dircategory", "titlepage", "copying", "insertcopying", "ignore", "verbatiminclude",
        "float", "caption", "shortcaption", "listoffloats",
        // Inline markup
        "code", "emph", "var", "file", "samp", "command", "strong", "dfn", "option", "env",
        "b", "i", "t", "r", "w", "key", "kbd", "cite", "email", "url", "uref", "dots",
        "enddots", "tie", "minus", "copyright", "registeredsymbol", "result", "expansion",
        "arrow", "print", "error", "equiv", "point", "sup", "sub", "euro", "pounds",
        "textdegree", "leq", "geq", "LaTeX", "TeX", "bullet", "comma", "footnote",
        "footnotestyle", "center", "sc", "verb", "acronym", "abbr", "asis", "slanted",
        "sansserif", "titlefont", "dmn", "math", "inlinefmt", "inlineifformat", "inlineraw",
        "inlinefmtifelse", "U", "clicksequence", "click", "indicateurl", "kbdinputstyle",
        // Named glyphs and letters
        "quoteleft", "quoteright", "quotedblleft", "quotedblright", "guillemetleft",
        "guillemetright", "guillemotleft", "guillemotright", "quotesinglbase", "quotedblbase",
        "exclamdown", "questiondown", "aa", "AA", "ae", "AE", "o", "O", "oe", "OE", "ss",
        "l", "L", "ordf", "ordm", "dotaccent", "ringaccent", "tieaccent", "u", "ubaraccent",
        "udotaccent", "v", "H", "dotless", "today",
        // Cross references and indices
        "ref", "xref", "pxref", "anchor", "xrefautomaticsectiontitle", "cindex", "findex",
        "vindex", "kindex", "pindex", "tindex", "printindex", "syncodeindex", "synindex",
        "defindex", "defcodeindex", "sortas", "seealso", "seeentry", "subentry",
        // Conditionals, flags and raw formats
        "iftex", "ifnottex", "ifhtml", "ifnothtml", "ifinfo", "ifnotinfo", "ifplaintext",
        "ifnotplaintext", "ifxml", "ifnotxml", "ifdocbook", "ifnotdocbook", "iflatex",
        "ifnotlatex", "ifset", "ifclear", "ifcommanddefined", "ifcommandnotdefined",
        "set", "clear", "value", "tex", "html", "xml", "docbook", "latex",
        // Macro machinery
        "macro", "rmacro", "unmacro", "alias", "definfoenclose",
        // Definition commands
        "deffn", "deffnx", "defun", "defunx", "defmac", "defmacx", "defspec", "defspecx",
        "defvr", "defvrx", "defvar", "defvarx", "defopt", "defoptx", "deftypefn", "deftypefnx",
        "deftypefun", "deftypefunx", "deftypevr", "deftypevrx", "deftypevar", "deftypevarx",
        "deftp", "deftpx", "defcv", "defcvx", "defivar", "defivarx", "defop", "defopx",
        "defmethod", "defmethodx", "deftypecv", "deftypecvx", "deftypeivar", "deftypeivarx",
        "deftypeop", "deftypeopx", "deftypemethod", "deftypemethodx",
        // Document control
        "include", "settitle", "setfilename", "documentencoding", "documentlanguage",
        "documentdescription", "contents", "shortcontents", "summarycontents", "image",
        "sp", "page", "need", "noindent", "indent", "afourpaper", "afivepaper", "afourlatex",
        "afourwide", "smallbook", "pagesizes", "finalout", "allowcodebreaks", "hyphenation",
        "setcodequotes", "codequoteundirected", "codequotebacktick", "frenchspacing",
        "firstparagraphindent", "paragraphindent", "exampleindent", "headings",
        "setchapternewpage", "raisesections", "lowersections", "everyheading", "everyfooting",
        "evenheading", "evenfooting", "oddheading", "oddfooting", "shorttitlepage", "title",
        "subtitle", "author", "vskip", "novalidate", "fonttextsize", "deftypefnnewline",
        "urefbreakstyle", "microtype", "end", "bye", "c", "comment"
    };

    /// <summary>True when the name is a genuine GNU Texinfo command.</summary>
    /// <param name="name">A command name without the leading <c>@</c>.</param>
    public static bool Contains(string name) => Names.Contains(name);
}
