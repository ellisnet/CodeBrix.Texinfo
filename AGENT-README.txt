================================================================================
AGENT-README: CodeBrix.Texinfo2Html
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Texinfo2Html.MitLicenseForever NuGet package
================================================================================


OVERVIEW
========

CodeBrix.Texinfo2Html is a fully managed, cross-platform .NET library that reads
a GNU Texinfo source file and renders it into HTML and CSS. The markup it emits
is written for PDF generation rather than for the browser: it stays inside the
documented HTML and CSS subset that CodeBrix.PdfDocCreate.Html2Pdf understands,
so the output is ready to be handed straight to that library - or to
CodeBrix.Texinfo2Pdf, the sibling package that does exactly that in one call.

It targets .NET 10 or later.

It reads two input dialects:

    .texi     Standard GNU Texinfo source files.
    .tely     The Texinfo dialect produced by LilyPond and CodeBrix.LilyPort,
              in which Texinfo markup is interleaved with LilyPond music
              snippets.

Provenance: this is an original implementation of a reader for the Texinfo file
format. It is not a port and contains no code from the GNU Texinfo project or
from any other Texinfo implementation.

The whole pipeline is in place and the public API is complete. Rendering never
throws over the contents of a document: anything unsupported, malformed or
missing becomes a warning in the result plus the nearest readable degradation.
The public surface is twelve types in one namespace: six for rendering a
document, six making up the music-snippet seam. Everything else is internal.

OTHER PACKAGES IN THIS REPOSITORY
---------------------------------

    CodeBrix.Texinfo2Pdf.MitLicenseForever  (MIT)
        Texinfo source -> PDF in one step, built on this package plus
        CodeBrix.PdfDocCreate.Html2Pdf. Its GenerateHtml methods hand back the
        very same TexinfoHtmlResult documented here, so a consumer who installs
        the PDF package still has the whole intermediate and does not need this
        package as a separate reference.
        See src/CodeBrix.Texinfo2Pdf/AGENT-README.txt
        (https://github.com/ellisnet/CodeBrix.Texinfo/blob/main/src/CodeBrix.Texinfo2Pdf/AGENT-README.txt)


INSTALLATION
============

    dotnet add package CodeBrix.Texinfo2Html.MitLicenseForever

PackageId:      CodeBrix.Texinfo2Html.MitLicenseForever
Assembly:       CodeBrix.Texinfo2Html
Namespace:      CodeBrix.Texinfo2Html
License:        MIT
Dependencies:   none - nothing beyond .NET itself, on any operating system.
Requirements:   none. This package emits HTML and CSS and never rasterizes
                anything, so it needs no native assets on any platform. Neither
                does the PDF stage - see below.

The package id carries the ".MitLicenseForever" suffix but the assembly and the
namespace do NOT - they are simply CodeBrix.Texinfo2Html. The suffix is a
CodeBrix family convention that records the license the package will always be
published under.

Install this package on its own when you want the intermediate HTML and CSS, or
when you want to post-process the markup before it is rendered to PDF. Install
CodeBrix.Texinfo2Pdf.MitLicenseForever instead when you want a PDF; it depends
on this package and brings it along.

If you hand this package's output to CodeBrix.PdfDocCreate.Html2Pdf yourself,
that stage needs nothing extra either: it is fully managed, draws SVG with
CodeBrix.Imaging.Drawing.NoSkia, and places SVG pictures into the PDF as vector
content by default. The Texinfo2Pdf AGENT-README explains it in full.


KEY NAMESPACES / USINGS
=======================

    using CodeBrix.Texinfo2Html;    //every public type of this package

Every public type sits in that one root namespace. The sub-namespaces underneath
it (Sources, Lexing, Preprocessing, Parsing, Model, Semantics, Snippets, Emit,
Diagnostics) are all internal. There is no CodeBrix.Texinfo namespace and no
CodeBrix.Texinfo assembly - that name belongs to the GitHub repository only.

The twelve public types:

    TexinfoHtmlRenderer         the entry point
    TexinfoHtmlOptions          settings, reachable as renderer.Options
    TexinfoHtmlResult           what a render returns
    TexinfoImageReference       one picture the markup refers to
    TexinfoRenderWarnings       everything that had to be degraded
    TexinfoConditionalProfile   enum: Print | Html

    ILilypondSnippetRenderer    the music-engraving seam (you implement it)
    LilypondSnippet             what an implementation is handed
    LilypondSnippetKind         enum: Music | LilypondFile | MusicXmlFile
    LilypondSnippetOptions      the snippet's bracketed options, typed
    LilypondSnippetResult       what an implementation returns
    LilypondSnippetImage        one picture inside that result


WHAT IT DOES WITH A DOCUMENT
============================

Beyond the structure of a manual - its sectioning tree, contents, block
environments and inline markup - the renderer settles the things a printed
document needs and an Info reader does not:

  * CROSS REFERENCES LINK. @ref, @xref and @pxref resolve through the document's
    anchor table to a #identifier link, with the wording Texinfo prescribes for
    each ("See ", "see ", nothing). A reference into another manual - one given
    an Info file or a printed manual name - is rendered as text and is not a
    fault. Names are matched with their whitespace collapsed, because a name
    written in braces may be wrapped across lines wherever the paragraph needed
    it. Anything left unresolved produces ONE warning for the document, naming
    the count and the first one.

  * INDICES PRINT. @printindex builds the named index from the entries
    collected during parsing - which come from the index commands, from the
    terms of an @ftable or @vtable, and from every definition command - after
    applying the @syncodeindex and @synindex merges, sorting by @sortas key
    where one is given, and honouring the
    txiindexbackslashignore / txiindexhyphenignore / txiindexlessthanignore /
    txiindexatsignignore flags. A document defines an index of its own with
    @defindex or @defcodeindex, and the @NAMEindex command that files into it
    comes into existence with it; @defcodeindex is the one whose entries print
    in a fixed-width font. Entries are grouped under letter headings, set in a
    fixed-width font for every predefined index except the concept one, and
    each line links to a marker left where the entry was written AND names the
    section it came from - a printed index has no page numbers to give, so the
    section name is what tells two identically worded entries apart. Markers
    are only emitted for entries an index actually prints.

  * FOOTNOTES SIT AT THE END OF THEIR CHAPTER. Each note is filed under the
    outermost sectioning unit containing it, skipping @top and @part; notes
    written outside any unit are printed at the end of the document. Numbering
    is document-wide, so a marker is unambiguous wherever the note ends up.

  * THE TEXT CONVENTIONS ARE APPLIED. --- is an em dash, -- an en dash, `` and
    '' are the directed double quotes, and ` and ' the single ones - in running
    prose only. Code-like contexts (@code, @samp, @kbd, @file, @option, @env,
    @command, @key, @t, @verb, @example, @lisp, @verbatim and music snippets)
    keep every character as written, which is what the Texinfo manual requires
    and what keeps --verbose from becoming an en dash. @display and @format are
    preformatted but not code, so their prose IS converted.

  * DEFINITIONS ARE TAKEN APART AND FILED. @deffn and its twenty-one relatives
    are read as a run of words - a braced group counting as one, so
    {Special Form} is a single category - laid out as category, class, data
    type, name, and then whatever is left as the arguments. A lone @ ending a
    line continues the heading, which is the one place in Texinfo where it
    does. Each x form (@deffnx and the rest) adds another heading line to the
    definition already open, however far down the body it is written. The name
    is filed in the index the Texinfo manual names for that command - functions
    for the @deffn side, variables for the @defvr side, data types for @deftp -
    and an entry for something belonging to a class names the class too
    ("border-pattern of Window", "expose on Window"), because two classes
    routinely define a member of the same name. @defline and @deftypeline
    inside @defblock write the same heading lines and file nothing, which is
    what they exist for.

    Texinfo prints the category out at the right margin. That needs a floating
    box the output subset has not got, so it is written at the head of the line
    instead - where the Info output puts it, and where it still labels what
    follows. A definition may sit inside lists, quotations and other
    definitions, so it is never laid out as a table.

  * FLOATS ARE NUMBERED AND CAPTIONED. A @float counts within its chapter and
    within its own type, so a manual runs Figure 1.1, Figure 1.2, Table 1.1 and
    starts again at Figure 2.1. A float in an unnumbered chapter has no stem to
    build on and counts straight through the document instead. A cross
    reference to a float's label reads as the type and number - "see
    Figure 1.2" - because the label itself would tell a reader nothing.
    @listoffloats prefers @shortcaption where there is one.

  * PICTURES TRAVEL WITH THE DOCUMENT. See IMAGES below.

  * MUSIC IS READ, AND ENGRAVED IF ANYTHING CAN. See MUSIC SNIPPETS below.

  * ACCENTS COMPOSE. Each accent command puts its combining mark on the
    character it applies to and the result is normalized, so what reaches the
    output is the precomposed character wherever Unicode has one - which is
    what the font packages carry a glyph for. Where there is none (an
    underbarred 'a'), the composed pair stands, because dropping the mark would
    be worse. The punctuation accents may be written with or without braces;
    the alphabetic ones need them.

  * A CHAPTER STARTS ON A FRESH PAGE. That is Texinfo's own default and what a
    printed manual looks like; @setchapternewpage off turns it off.

  * NESTED PREFORMATTED BLOCKS BECOME INDENTATION. @example inside @display is
    legal Texinfo, and there is no nested <pre> in the output subset - nor does
    there need to be one, because the text is already whitespace-preserved. The
    inner block is written as one more step of indentation (five spaces, the
    step Texinfo's own printed output uses), which is what the nesting means.
    The inner block keeps its OWN text conventions, so an @example inside a
    @display still has its dashes left alone while the prose around it is
    converted. Indentation is written when a line turns out to have content,
    never on the line break that ended the one before, so blank lines stay
    blank and nothing trails. A paragraph directive written inside a
    preformatted block (@noindent, @indent) is dropped, line and all.

  * A LINE MACRO READS A WHOLE LINE. @linemacro defines a macro that is called
    as a line command, and its argument rules are unlike every other invocation
    form in Texinfo: arguments are separated by SPACES rather than commas, a
    pair of braces enclosing an argument is removed, an empty argument has to be
    written as {}, and the last argument takes the whole rest of the line so it
    may hold spaces unbraced. A brace after the macro name therefore opens the
    first ARGUMENT and is not a brace-form argument list. A line whose last
    character is a lone @ continues onto the next one, and that @ and its
    newline stay inside the argument, because what the expansion has to produce
    is a valid definition line. The reason the command exists is to let a
    manual define its own definition commands on top of @defline and
    @deftypeline, so it earns its keep only alongside those.


COVERAGE AND SCALE - WHAT TO EXPECT OF A REAL MANUAL
====================================================

There is no list of supported commands to check a document against, because the
answer is not a list: what a document uses that this library does not implement
becomes a WARNING and the nearest readable degradation, never an exception and
never a lost document. So the useful question is not "is @foo supported" but
"how many warnings does my manual produce, and do I mind them". Render it and
read result.Warnings - that is the intended way to find out.

What the library is actually run against, every time its test suite runs:

  * THE ENGLISH LILYPOND DOCUMENTATION SET - eight manuals, about 110,000 lines
    across 123 files, the largest of them the 51,000-line notation reference.
    All eight parse and render, and every internal link in the output has a
    destination in the same document. This corpus leans hard on @macro (its own
    two macro files define 158 of them), on the .tely music environments, and
    on accented and Cyrillic text.
  * THE GNU TEXINFO MANUAL - the language's own manual, and the widest use of
    general-Texinfo commands there is. It renders with four warnings, every one
    a thing this library declines to do rather than cannot: three skipped @tex
    blocks and one @math.
  * THE GNU MAKE MANUAL - renders with NO warnings at all.

ENCODING IS UTF-8, OR A BYTE ORDER MARK. Source is read as UTF-8 unless a BOM
says otherwise, and invalid bytes become replacement characters rather than an
exception. @documentencoding is READ AND REPORTED, not obeyed: a document
declaring anything but UTF-8 or US-ASCII gets one warning under the Encoding
category and is still read as UTF-8. A Latin-1 manual therefore needs converting
before it is rendered - the warning is there to say so rather than to let the
accented characters come out wrong in silence.

Reading and parsing even the 51,000-line notation reference is a fraction of a
second; see PERFORMANCE TIPS below for the numbers.


MUSIC SNIPPETS (the .tely dialect)
==================================

@lilypond (block form and brace form), @lilypondfile and @musicxmlfile are
parsed, their bracketed options are read into named properties, and the whole
snippet is offered to ILilypondSnippetRenderer if the caller registered one on
Options.SnippetRenderer.

WITH NO RENDERER - the default - the snippet is shown as its source text in a
preformatted block (<pre class="texinfo-lilypond">) and ONE warning records how
many there were. That is not a placeholder to be removed later: engraving music
means running LilyPond, and this library will not take on that dependency.

WITH A RENDERER, the pictures it returns are placed and are registered in
result.Images, so they travel with the document exactly as @image pictures do -
including pictures handed over as bytes, which WriteToDirectory writes out.

    var renderer = new TexinfoHtmlRenderer();
    renderer.Options.SnippetRenderer = new MyEngraver();

Two option names decide what the DOCUMENT does; every other option is the
engraver's business and is simply passed along:

    verbatim   show the source as well as the engraving. The source is written
               ABOVE the picture - input first, then what it produces.
    quote      indent the snippet. Written as an inline style on the element,
               not as a wrapping container, so a snippet inside @quotation
               stays inside the quotation's own layout.

The option vocabulary is the one MEASURED across the whole English LilyPond
documentation set, not one invented from the syntax: quote, verbatim, inline,
notime, texidoc, doctitle, noindent, ragged-right, noragged-right, fragment,
nofragment, relative[=N], staffsize=, line-width=, indent=, papersize=,
paper-width=, paper-height=. Anything else is kept as written in Options.All,
listed in Options.Unrecognized, and reported once for the document - a renderer
that knows an option this library does not can still act on it. Dimensions are
kept as strings ("3\cm", "6\in") because they are LilyPond's own units and mean
nothing outside an engraver; do not convert them.

A file named by @lilypondfile or @musicxmlfile is looked for on the document's
search path (the source directory, its parent, then IncludeSearchPaths). A name
written without an extension is tried with .ly, .xml, .musicxml and .mxl in
turn. A rooted path is used as it stands.

Things about this seam that are easy to get wrong:

  * AN IDENTICAL SNIPPET IS ENGRAVED ONCE. The cache key is everything the
    renderer is given (kind, file name, resolved path, source text and the
    option list as written), so reuse can never change what is shown. The
    notation reference holds over 1,600 snippets and repeats many of them.
  * A RENDERER THAT THROWS IS CAUGHT and turned into the failure it should have
    returned (the exception type name and message become the failure text). A
    document must not be lost to a consumer's exception. A renderer that
    returns null is treated as NotRendered.
  * @lilypondfile WITH verbatim READS THE FILE - the one place this library
    opens a music file rather than only naming it - and starts at the file's
    "% begin verbatim" marker when it has one. Every one of the 389 snippet
    files in the LilyPond documentation carries that marker at the end of its
    \header block, which is exactly the boilerplate it exists to skip. A file
    that is missing is only reported when a renderer was registered; with none,
    the document never needed it.
  * EVERY KIND OF SNIPPET TROUBLE IS COUNTED, NOT LISTED. Snippets shown as
    source, renderer failures, missing music files and unrecognized options
    each produce ONE warning for the whole document, naming the count and the
    first case - so a misconfigured engraver cannot bury every other warning
    under thousands of its own.

The implementer's contract, with signatures, is under CORE API REFERENCE; a
complete implementation is under COMPLETE EXAMPLES.


IMAGES
======

An @image reference names a file with no directory and usually no extension, so
the file is searched for and its extension probed - every format the PDF stage
can place (.png .jpg .jpeg .gif .bmp .svg .webp .tif .tiff .tga .ppm .pgm .pbm,
plus any extension the command declares). .pdf is deliberately NOT probed: a
manual that keeps pdf/NAME variants for its TeX branch would otherwise hand the
PDF stage a file it cannot decode.

This package only NAMES pictures; it never decodes or rasterizes them. What the
markup writes is NOT where the file was found. It is a path relative to the
document itself - <ImageFolderName>/<file> - so the generated document is
complete once its pictures are beside it:

    WriteToDirectory(dir)   writes the markup and the stylesheet AND copies
                            every picture into dir, which is everything the
                            document needs.
    CopyImagesTo(dir)       does only the copying, for a caller that renders
                            result.Html from memory. Pass the same dir to
                            the PDF stage as its base directory.
    result.Images           the whole mapping: SourcePath -> RelativePath.

Two pictures with the same file name from different directories are numbered
apart. A picture that cannot be found becomes its alternate text in grey
italic (class "texinfo-missing-image"), plus one warning under the Include
category.

WHERE PICTURES ARE LOOKED FOR, in order:

    1. the source file's directory       (GenerateFromFile) / baseDirectory
                                          (Generate)
    2. that directory's parent
    3. each entry of Options.IncludeSearchPaths, in list order
    4. each entry of Options.ImageSearchPaths, in list order

For each directory every candidate extension is tried before moving to the
next directory. Relative entries in the two lists are resolved against the
process's current directory, so prefer absolute paths there.


CORE API REFERENCE
==================

Twelve public types, all in the CodeBrix.Texinfo2Html namespace.

TexinfoHtmlRenderer
-------------------

    var renderer = new TexinfoHtmlRenderer();

    TexinfoHtmlOptions Options { get; }
    TexinfoHtmlResult  GenerateFromFile(string texinfoFilePath)
    TexinfoHtmlResult  Generate(string texinfoSource, string baseDirectory = null)

One renderer can be reused for many documents; set Options before calling, and
note that it is NOT safe across threads - give each thread its own renderer.
Rendering never throws over the contents of a document - anything unsupported,
malformed or missing becomes a warning in the result plus the nearest readable
degradation. Exceptions are reserved for the caller's own mistakes:

    ArgumentException         GenerateFromFile given a null or blank path
    FileNotFoundException     GenerateFromFile given a file that is not there
    ArgumentNullException     Generate given a null source string

GenerateFromFile seeds the search paths with the source file's directory AND
that directory's parent, in that order, which is what lets a manual written as
a tree of @include files render from its top-level source, and what lets
@image{pictures/foo} resolve from a sibling directory. It also derives the
stylesheet name, the image folder name and the default output base name from
the source file's name.

Generate reads source held in memory. Its baseDirectory is what @include,
@image and the music-file commands resolve against; pass null when the source
needs no files of its own. The derived names are then texinfo.css,
texinfo-images/ and "index".

TexinfoHtmlOptions
------------------

    bool                       EmitSingleFile      { get; set; }   (false)
    TexinfoConditionalProfile  ConditionalProfile  { get; set; }   (Print)
    List<string>               IncludeSearchPaths  { get; }        (empty)
    List<string>               ImageSearchPaths    { get; }        (empty)
    Dictionary<string,string>  PredefinedValues    { get; }        (empty; ordinal keys)
    bool                       NumberSections      { get; set; }   (true)
    string                     ExtraCss            { get; set; }   ("")
    string                     CssFileName         { get; set; }   ("" - derived)
    string                     ImageFolderName     { get; set; }   ("" - derived)
    ILilypondSnippetRenderer   SnippetRenderer     { get; set; }   (null)

EmitSingleFile   true embeds the stylesheet in the HTML; false (default) links
                 to a stylesheet file beside it, the easier pair to restyle by
                 hand. result.Css is populated either way.

IncludeSearchPaths   EXTRA directories searched by @include, @lilypondfile and
                 @musicxmlfile, AFTER the source file's directory and that
                 directory's parent, which are always searched first. Entries
                 are tried in list order. Blank entries are ignored.

ImageSearchPaths   EXTRA directories searched for the files named by @image,
                 AFTER everything @include searches (so after the two
                 automatic directories and after IncludeSearchPaths). Each
                 directory is tried with every candidate extension. These
                 paths are NOT consulted for @include or for music files.

PredefinedValues   acts as though the source opened with @set name value for
                 each pair, which is how to supply the version and date strings
                 a manual's build normally generates into an included file.
                 Keys are compared ordinally (case-sensitive), as Texinfo flags
                 are.

NumberSections   false leaves every heading unnumbered.

ExtraCss         appended after the built-in stylesheet, so a repeated rule of
                 equal specificity wins. The built-in stylesheet uses the
                 generic families serif, sans-serif and monospace and carries
                 no @page rule.

CssFileName / ImageFolderName   derived from the source file's name when left
                 empty (manual.css and manual-images/ for manual.texi), or
                 texinfo.css and texinfo-images/ for a document rendered from a
                 string. Naming the folder after the document keeps two manuals
                 written into one directory from arguing over a picture they
                 both call logo.png. A document with no pictures never creates
                 the folder.

SnippetRenderer  see MUSIC SNIPPETS above.

TexinfoHtmlResult
-----------------

    string Html            //the complete document
    string BodyHtml        //the generated markup on its own
    string Css             //always separate, even when it was embedded
    string Title           //from @settitle; "" when there was none
    string Author          //from the title page's FIRST @author; "" when none
    string BaseDirectory   //the directory the source was read from
    string CssFileName     //the name the markup links to
    IReadOnlyList<TexinfoImageReference> Images
    TexinfoRenderWarnings Warnings

    string ToHtmlDocument(string replacementCss)
    string WriteToDirectory(string directory, string baseName = null)
    int    CopyImagesTo(string directory)

Html is a complete document: with the stylesheet embedded when EmitSingleFile
was set, otherwise linking to CssFileName. BodyHtml is for a caller assembling
a page of their own around the generated markup. A title page naming several
authors still prints them all, but only the first is reported in Author.

ToHtmlDocument rebuilds the complete document around a stylesheet of the
caller's own, embedded in it - the hand-off point for restyling: take Css,
change it or replace it outright, and pass it back here. Null is treated as an
empty stylesheet.

WriteToDirectory creates the directory if needed, writes <baseName>.html (UTF-8,
no byte order mark), writes the stylesheet beside it under CssFileName unless
EmitSingleFile was set, and copies the document's pictures into it. baseName
defaults to the source file's name, or to "index" for a document rendered from
a string. It returns the full path of the HTML file. Throws ArgumentException
for a blank directory.

CopyImagesTo puts every picture under the relative paths the markup uses,
creating folders as needed, and returns how many files it wrote. Pictures found
on disk are copied (overwriting); ones a snippet renderer handed over as bytes
are written. A picture whose source file no longer exists, or whose destination
is its own source, is skipped without a count. A document with no pictures
creates nothing.

TexinfoImageReference
---------------------

    string SourcePath      //where the file was found at render time; "" for a
                           //picture a snippet renderer handed over as bytes
    string RelativePath    //what the markup points at, forward slashes
    bool   IsGenerated     //engraved from a snippet rather than named by @image
    bool   HasContent      //held in memory; there is no file to copy from
    byte[] GetContent()    //a copy of those bytes, or an empty array

TexinfoRenderWarnings
---------------------

    IReadOnlyList<string> Messages   //in the order the run produced them
    int                   Count

Every message has the shape

    <Category>: <message> (at <source>:<line>:<column>)

where <source> is the file path, or a macro-expansion description for text that
came out of a macro. The leading category word is what to filter on. There are
ten categories:

    Include          an @include file, an @image file or a music file named by
                     a snippet could not be found on the search path, could
                     not be read, or includes itself; also "@include" with no
                     file name.
    Conditional      a conditional block (@ifset, @iftex, @ifnotinfo, ...) is
                     malformed or unbalanced - a missing @end, an @end with no
                     opening, a block naming no flag or no format.
    Macro            a macro definition, redefinition, alias or expansion
                     problem (@macro, @rmacro, @linemacro, @alias) - a
                     circular alias chain, an alias that would shadow a
                     built-in, a call missing arguments.
    Value            a @value{name} whose flag was never set (the text
                     "{No value for 'name'}" is left in the output), or a
                     malformed @set / @value.
    RawBlockSkipped  a raw output block (@tex, @html, @docbook, ...) or an
                     @inlineraw was skipped because its content bypasses the
                     HTML subset this library targets. Expected on most real
                     manuals; usually harmless.
    Encoding         @documentencoding declared something other than UTF-8 or
                     US-ASCII; the text was still read as UTF-8.
    Syntax           a lexical or structural problem in the source itself - a
                     block missing its @end, "@" at end of input, @verb with
                     no brace group. The most numerous category in the code
                     base, and the one that means the document itself needs
                     fixing.
    UnknownCommand   a command the parser does not implement. Its argument
                     text or block content is kept, so the words survive even
                     though their meaning is lost.
    Reference        a node, anchor or cross-reference problem: ONE message
                     counting the cross references that name a destination the
                     document does not define (and naming the first), a
                     destination with no name, or a name defined more than
                     once (the first definition is kept).
    Emit             something the emitter understood but could not render as
                     the source intended and rendered some other way: @math
                     set as styled text, @printindex or @listoffloats that
                     printed nothing, a non-preformatted environment inside a
                     preformatted one, and the four counted music-snippet
                     conditions (shown as source, renderer failed, unrecognized
                     options; a missing music file is Include). Kept apart
                     from UnknownCommand so a document that parsed cleanly can
                     be told from one that did not.

Filtering by category is a string prefix test:

    using System.Linq;

    var syntax = result.Warnings.Messages
        .Where(m => m.StartsWith("Syntax:", StringComparison.Ordinal))
        .ToList();

    //everything except the expected raw-block skips
    var surprises = result.Warnings.Messages
        .Where(m => !m.StartsWith("RawBlockSkipped:", StringComparison.Ordinal))
        .ToList();

There is no structured (enum-typed) warning object on this package's public
surface; the category word at the head of the message is the contract.

TexinfoConditionalProfile
-------------------------

    Print   //@iftex and every @ifnot... branch; the right one for PDF output
    Html    //@ifhtml on, @ifnothtml off; every other format off, its
            //@ifnot... branch on

Print deliberately reads BOTH @iftex and @ifnottex, because real manuals put
document structure - most often the @node Top and @top pair the whole document
hangs from - in the @ifnottex branch, and keep their TeX-only machinery in raw
@tex blocks, which are skipped anyway. The cost is that a document writing the
same visible content into both branches contributes it twice. Raw output blocks
(@tex, @html, ...) are skipped with a RawBlockSkipped warning under EITHER
profile.

ILilypondSnippetRenderer - the implementer's contract
-----------------------------------------------------

    public interface ILilypondSnippetRenderer
    {
        LilypondSnippetResult Render(LilypondSnippet snippet);
    }

Called once for each DISTINCT snippet (see the cache note under MUSIC
SNIPPETS), on the thread that is rendering the document. Return the pictures
it engraved to; LilypondSnippetResult.NotRendered to decline quietly (the
document falls back to showing the source and no warning is raised - declining
is a decision, not a fault); or LilypondSnippetResult.Failed(message) to report
why it could not be done (source is shown and one counted warning names the
reason). Never throw - an escaping exception is caught and turned into a
failure, but a returned failure says what went wrong far better than a stack
trace does.

LilypondSnippet (what an implementation is handed; immutable, created only by
this library - there is no public constructor)

    LilypondSnippetKind    Kind           //Music | LilypondFile | MusicXmlFile
    string                 Source         //the music as written, for Kind ==
                                          //Music; "" for the file kinds
    string                 FileName       //as the document wrote it, for the
                                          //file kinds (e.g. "included/bar.ly");
                                          //"" for Music
    string                 FilePath       //full path FileName was found at on
                                          //the search path; "" when NOT found
    LilypondSnippetOptions Options        //the bracketed options, typed
    bool                   IsInline       //written inside a paragraph (brace
                                          //form) rather than standing alone
    string                 BaseDirectory  //directory the document was read
                                          //from; "" for a string with none
    string                 SourceFile     //file the snippet was written in
    int                    LineNumber     //line it started on, counting from 1
    string                 ToString()

Source is passed through exactly as the document wrote it; it was captured raw
and never treated as Texinfo, so an @ or a brace inside it means whatever
LilyPond says it means. IsInline says where the snippet SITS; Options.Inline is
the option asking for a small engraving. They are different questions and a
renderer usually wants both. FilePath is empty when the named file was not
found - which is not an error here, because a manual may name a file its build
generates - and a renderer handed an empty path should decline rather than
guess.

LilypondSnippetKind

    Music          //@lilypond, block form or brace form; source in Source
    LilypondFile   //@lilypondfile{name}; a LilyPond file to engrave
    MusicXmlFile   //@musicxmlfile{name}; a MusicXML file to convert and engrave

LilypondSnippetOptions (read-only to a consumer; every setter is internal)

    bool     Quote        //quote: indent from the margin (acted on by the
                          //document, passed along too)
    bool     Verbatim     //verbatim: show the source as well (acted on by the
                          //document)
    bool     Inline       //inline: a small fragment for the run of the text
    bool     NoTime       //notime: omit the time signature
    bool     TexiDoc      //texidoc: the file's own documentation text is
                          //wanted; only a renderer can read it
    bool     DocTitle     //doctitle: the file's own title is wanted
    bool     NoIndent     //noindent, which is indent=0 said as a flag
    bool?    RaggedRight  //true from ragged-right, false from noragged-right,
                          //null when neither was written
    bool?    Fragment     //true from fragment, false from nofragment, null
                          //when neither
    int?     Relative     //relative=N; bare "relative" counts as 1; null
                          //when absent
    double?  StaffSize    //staffsize=N (fractional allowed); null when absent
    string   LineWidth    //line-width= exactly as written ("3\cm"); "" absent
    string   Indent       //indent= as written; "" when absent
    string   PaperSize    //papersize= ("a5", "a8landscape"); "" when absent
    string   PaperWidth   //paper-width= as written; "" when absent
    string   PaperHeight  //paper-height= as written; "" when absent
    IReadOnlyList<string> All           //every option exactly as written, in
                                        //the order written, recognized or not
    IReadOnlyList<string> Unrecognized  //the ones no property above names;
                                        //still present in All
    string   ToString()   //"(no options)" or the comma-joined All list

A renderer that understands more than the named properties should read All
rather than being limited by them.

LilypondSnippetResult (what an implementation returns; construct only through
the factories)

    static LilypondSnippetResult NotRendered { get; }
    static LilypondSnippetResult FromFile(string imagePath)
    static LilypondSnippetResult FromContent(byte[] content, string fileExtension)
    static LilypondSnippetResult FromImages(IEnumerable<LilypondSnippetImage> images)
    static LilypondSnippetResult Failed(string message)

    IReadOnlyList<LilypondSnippetImage> Images        //in placement order
    string                              ErrorMessage  //"" when nothing went wrong
    bool                                IsRendered    //Images.Count > 0
    bool                                IsFailure     //ErrorMessage.Length > 0

FromFile and FromContent carry ONE picture. FromImages carries several, placed
in the order given, which is what a score longer than one page engraves to;
null entries are dropped, and a null sequence throws ArgumentNullException.
Failed with a null or blank message substitutes "The snippet could not be
engraved."; otherwise the message is trimmed and kept.

LilypondSnippetImage (one picture; construct only through the factories)

    static LilypondSnippetImage FromFile(string filePath)
    static LilypondSnippetImage FromContent(byte[] content, string fileExtension)

    string FilePath        //the file the renderer wrote; "" for bytes
    string FileExtension   //the format, always with its leading dot
    bool   HasContent      //given as bytes rather than as a file
    byte[] GetContent()    //a copy of the bytes, or an empty array for a file

FromFile takes the extension from the file name; a null or blank path throws
ArgumentException. FromContent's fileExtension may be written with or without
its dot ("png", ".svg"); it is what the picture is written under, so it has to
say what the bytes actually are. A null content array throws
ArgumentNullException; a blank extension throws ArgumentException. A picture
the renderer wrote to disk keeps its own file name inside the image folder;
one given as bytes is named by the library.


COMPLETE EXAMPLES
=================

(1) Texinfo file -> HTML + CSS + pictures on disk (this package's headline
    output, nothing else involved)

    using System;
    using CodeBrix.Texinfo2Html;

    internal static class Program
    {
        private static void Main(string[] args)
        {
            var renderer = new TexinfoHtmlRenderer();
            renderer.Options.PredefinedValues["VERSION"] = "2.1";  //as @set VERSION 2.1
            renderer.Options.ExtraCss = "h1 { color: #003366; }";

            TexinfoHtmlResult result = renderer.GenerateFromFile("docs/manual.texi");

            Console.WriteLine($"Title: {result.Title}  Author: {result.Author}");
            Console.WriteLine($"{result.Images.Count} picture(s), {result.Warnings.Count} warning(s)");
            foreach (string warning in result.Warnings.Messages)
            {
                Console.WriteLine("  " + warning);
            }

            //out/manual.html + out/manual.css + out/manual-images/... - a
            //complete document, ready to open or to hand to a PDF renderer.
            string htmlPath = result.WriteToDirectory("out");
            Console.WriteLine("Wrote " + htmlPath);
        }
    }

(2) Source held in memory, as one self-contained file

    using System.IO;
    using CodeBrix.Texinfo2Html;

    var renderer = new TexinfoHtmlRenderer();
    renderer.Options.EmitSingleFile = true;      //stylesheet embedded in Html
    renderer.Options.NumberSections = false;

    string source = "@settitle Notes\n@node Top\n@top Notes\n\n"
        + "@chapter First\nHello, @emph{world}.\n";

    //null base directory: this source names no files of its own
    TexinfoHtmlResult result = renderer.Generate(source, null);
    File.WriteAllText("notes.html", result.Html);   //one file is the whole document

    //the same document written by the library: notes/index.html, no .css file
    result.WriteToDirectory("notes");

(3) Restyle before writing

    using CodeBrix.Texinfo2Html;

    TexinfoHtmlResult result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
    string myCss = result.Css.Replace("#111111", "#000033");   //or replace wholesale
    string html = result.ToHtmlDocument(myCss);                //self-contained
    result.CopyImagesTo("out");                                //pictures beside it
    System.IO.File.WriteAllText("out/manual.html", html);

(4) Engraving the music of a .tely manual - a complete ILilypondSnippetRenderer

    using System;
    using System.IO;
    using CodeBrix.Texinfo2Html;

    public sealed class MyEngraver : ILilypondSnippetRenderer
    {
        public LilypondSnippetResult Render(LilypondSnippet snippet)
        {
            if (snippet.Kind != LilypondSnippetKind.Music && snippet.FilePath.Length == 0)
            {
                return LilypondSnippetResult.NotRendered;   //file not found: decline
            }
            if (snippet.Kind == LilypondSnippetKind.MusicXmlFile)
            {
                return LilypondSnippetResult.Failed("MusicXML is not supported by this engraver.");
            }

            //snippet.Source (Music) or snippet.FilePath (LilypondFile), plus
            //snippet.Options.Relative, .Fragment, .RaggedRight, .LineWidth,
            //.StaffSize, .Options.All ... - hand them to your LilyPond driver.
            byte[] png = MyLilypondDriver.EngraveToPng(snippet);
            if (png == null)
            {
                return LilypondSnippetResult.Failed("LilyPond produced no output.");
            }
            return LilypondSnippetResult.FromContent(png, "png");

            //Alternatives:
            //  return LilypondSnippetResult.FromFile("/tmp/engraved/score-1.png");
            //  return LilypondSnippetResult.FromImages(new[] {
            //      LilypondSnippetImage.FromFile("/tmp/engraved/page-1.svg"),
            //      LilypondSnippetImage.FromFile("/tmp/engraved/page-2.svg") });
        }
    }

    var renderer = new TexinfoHtmlRenderer();
    renderer.Options.SnippetRenderer = new MyEngraver();
    TexinfoHtmlResult result = renderer.GenerateFromFile("notation.tely");
    result.WriteToDirectory("out");   //engraved pictures land in out/notation-images/

(5) Warnings as a build gate

    using System;
    using System.Linq;
    using CodeBrix.Texinfo2Html;

    TexinfoHtmlResult result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
    var real = result.Warnings.Messages
        .Where(m => !m.StartsWith("RawBlockSkipped:", StringComparison.Ordinal))
        .ToList();
    if (real.Count > 0)
    {
        Console.Error.WriteLine(string.Join(Environment.NewLine, real));
        Environment.Exit(1);
    }

(6) Driving CodeBrix.PdfDocCreate.Html2Pdf by hand

Reach for CodeBrix.Texinfo2Pdf.MitLicenseForever instead of doing this; it
gets the picture staging right and sweeps up after itself. But a consumer who
has this package plus CodeBrix.PdfDocCreate.Html2Pdf.MitLicenseForever and
wants a PDF does it like this:

    using CodeBrix.PdfDocCreate.Html2Pdf;
    using CodeBrix.Texinfo2Html;

    //(a) straight to a PDF
    var result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
    var htmlPath = result.WriteToDirectory("out");        //manual.html + manual.css
    new HtmlPdfRenderer().RenderFile(htmlPath, "out/manual.pdf");

    //(b) restyle the intermediate first
    var result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
    var myCss = result.Css.Replace("#111111", "#000033"); //or replace wholesale
    var html = result.ToHtmlDocument(myCss);
    result.CopyImagesTo("out");                           //see IMAGES above
    new HtmlPdfRenderer().RenderHtml(html, "out/manual.pdf", "out");

The base directory handed to Html2Pdf in workflow (b) is the directory the
pictures were copied into, not the directory the source was read from: the
markup's image paths are relative to the document, so those two have to agree.
Getting that wrong is the usual reason a hand-driven conversion loses its
pictures, and it is the thing CodeBrix.Texinfo2Pdf takes care of.


MINIMUM VIABLE PROJECT
======================

texi2html.csproj

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>disable</Nullable>
        <ImplicitUsings>disable</ImplicitUsings>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.Texinfo2Html.MitLicenseForever" Version="*" />
      </ItemGroup>
    </Project>

Program.cs

    using System;
    using CodeBrix.Texinfo2Html;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: texi2html <source.texi> <output-dir>");
                return 2;
            }
            TexinfoHtmlResult result = new TexinfoHtmlRenderer().GenerateFromFile(args[0]);
            string htmlPath = result.WriteToDirectory(args[1]);
            foreach (string warning in result.Warnings.Messages)
            {
                Console.Error.WriteLine(warning);
            }
            Console.WriteLine(htmlPath);
            return 0;
        }
    }

    dotnet run -- docs/manual.texi out

No native assets, no fonts, no other packages are needed on any operating
system. (Replace Version="*" with the current version from nuget.org when you
pin.)


PERFORMANCE TIPS
================

  * Cost is linear in document size. Reading and parsing the 51,000-line
    LilyPond notation reference is a fraction of a second; the six-or-so
    seconds an end-to-end PDF of it takes on a developer laptop are the
    typesetting, which is the PDF stage's, not this package's. The test suite
    renders synthetic manuals of 500 and 2,000 sections purely to fail if
    anything ever goes quadratic.
  * A reused renderer accumulates nothing between documents; reading the same
    manual twice through one renderer costs the same both times. Reuse one
    renderer per thread rather than constructing one per call, but do not
    share one across threads.
  * Snippet engraving is cached per distinct snippet for the duration of one
    render (see MUSIC SNIPPETS), so a manual that repeats a snippet does not
    pay for it twice. Keep an ILilypondSnippetRenderer cheap to call - it is
    invoked synchronously on the rendering thread.
  * WriteToDirectory copies every picture on each call; a caller that renders
    from memory and already has the pictures in place can skip CopyImagesTo.
  * A manual of any size is comfortable in a build step or an offline job.
    Rendering a 965-page manual inside a web request is not what any of this
    is for.


COMMON PITFALLS TO AVOID
========================

  * TREATING THE OUTPUT AS WEB MARKUP. The HTML and CSS are a contract with
    CodeBrix.PdfDocCreate.Html2Pdf's documented subset, not general-purpose
    web markup. It opens in a browser, but that is not what it is for; markup
    a browser tolerates is not automatically markup Html2Pdf renders, so keep
    any post-processing inside that subset.
  * LOSING THE PICTURES. The markup points at <ImageFolderName>/<file>
    RELATIVE TO THE DOCUMENT, not at where the files were found. Whatever
    directory the HTML lives in (or is handed to a PDF renderer as the base
    directory) must be the directory the pictures were copied into.
    WriteToDirectory does all of it; CopyImagesTo(dir) + the same dir as base
    directory is the manual equivalent.
  * EXPECTING result.Html TO LINK TO NOTHING. With EmitSingleFile false (the
    default), Html links to CssFileName; write the stylesheet beside it
    (WriteToDirectory does) or use ToHtmlDocument(result.Css) to embed it.
  * OPTIONS ARE LIVE. renderer.Options is the renderer's own object; whatever
    you set stays set for every later render through that renderer. Reset what
    you changed, or use a fresh renderer.
  * ONE RENDERER PER THREAD. TexinfoHtmlRenderer is not thread-safe.
  * EXPECTING EXCEPTIONS FOR BAD DOCUMENTS. Nothing about a document's
    contents throws. Read result.Warnings; a build that wants to fail on
    problems has to check it (see COMPLETE EXAMPLES (5)).
  * PATTERN-MATCHING WHOLE WARNING MESSAGES. Message prose is not a
    compatibility surface; the leading category word is. Filter on
    "Category:" prefixes.
  * EXPECTING MUSIC TO BE ENGRAVED. With no SnippetRenderer every snippet is
    its source text, by design; this package will never run LilyPond.
  * IMPLEMENTING ILilypondSnippetRenderer TO THROW. Return Failed(...) instead;
    a thrown exception still becomes a failure, but its message is worse.
  * CONFUSING snippet.IsInline WITH snippet.Options.Inline. The first is where
    the snippet sits; the second is a request for a small engraving.
  * ASSUMING @documentencoding IS OBEYED. It is reported, not obeyed; convert
    a Latin-1 manual to UTF-8 first.
  * ASSUMING A FILE PATH IN LilypondSnippet.FilePath. It is "" when the named
    file was not found; decline rather than guess.
  * RELATIVE ENTRIES IN IncludeSearchPaths / ImageSearchPaths resolve against
    the process's current directory, not the document. Use absolute paths.
  * EXPECTING .pdf VARIANTS OF PICTURES TO BE FOUND. .pdf is not probed (see
    IMAGES); a manual that only has pdf/NAME will report the image missing.
  * EXPECTING DOUBLE CONTENT NOT TO APPEAR under the Print profile when a
    manual writes the same visible text into both @iftex and @ifnottex. Both
    branches are read, deliberately.


WHAT THIS PACKAGE DOES NOT DO
=============================

  * NO PDF. This package stops at HTML and CSS. The PDF stage is
    CodeBrix.PdfDocCreate.Html2Pdf, wrapped for Texinfo by
    CodeBrix.Texinfo2Pdf.MitLicenseForever.
  * THE TARGET IS ONE PRINTED DOCUMENT. There is no Info output and no
    split-into-a-website HTML output. @menu is parsed and dropped, and node
    pointers (next, prev, up) are read and ignored, because a PDF is read front
    to back and has no navigation to build.
  * @math and @displaymath are styled text. There is no mathematical
    typesetter here and there is not going to be one.
  * @documentencoding is read and reported, NOT obeyed. Source is UTF-8 (or
    whatever a byte order mark says); see COVERAGE AND SCALE above.
  * Raw output blocks (@tex, @html, @docbook, ... and @inlineraw) are skipped
    with a RawBlockSkipped warning under either conditional profile.
  * A block environment that is NOT preformatted, written inside one that is -
    an @itemize inside an @display - is not rendered as itself. A list or a
    table has no representation inside preformatted text, so it degrades with a
    warning. A PREFORMATTED environment nested in another one does work; see
    NESTED PREFORMATTED BLOCKS above.
  * txidefnamenospace is read as it stood at the END of the document, so a
    document that sets and clears it around one definition gets the setting
    the last @set left. Setting it once, which is how it is meant to be used,
    works.
  * Nothing here engraves music. The seam to do it is defined and wired (see
    MUSIC SNIPPETS above), but this library will not take on a dependency on
    LilyPond, so with no renderer registered a snippet is its source text.
  * Nothing here decodes or rasterizes pictures. Pictures are found, named
    and copied; decoding is the PDF stage's job.
  * There is no structured warning object; warnings are strings with a
    category prefix.


WORKING EXAMPLES ON GITHUB
==========================

The test project for this package is the most complete set of working examples
of its API. Each file is written against a small original fixture document, so
every test is self-contained and readable on its own:

    https://github.com/ellisnet/CodeBrix.Texinfo/tree/main/tests/CodeBrix.Texinfo2Html.Tests

    TexinfoHtmlRendererTests.cs      the public API end to end: Generate and
                                     GenerateFromFile, EmitSingleFile,
                                     ExtraCss, ToHtmlDocument, WriteToDirectory
                                     (with and without a stylesheet file, with
                                     pictures), the exceptions for a blank or
                                     missing path, section numbering, cross
                                     references, indices, footnotes, images
    LilypondSnippetTests.cs          a complete fake ILilypondSnippetRenderer;
                                     no-renderer fallback, FromContent /
                                     FromFile / FromImages results, verbatim
                                     and quote placement, the options and
                                     position a renderer is given, brace-form
                                     IsInline, the "% begin verbatim" marker,
                                     a missing music file
    LilypondOptionParserTests.cs     every named snippet option and how bare,
                                     =value, fractional and unknown options
                                     parse
    DefinitionCommandTests.cs        @deffn and relatives: categories, typed
                                     forms, class members, x forms, index
                                     filing
    FloatTests.cs                    @float numbering, captions, @listoffloats,
                                     references to floats
    AccentCommandTests.cs            accent composition, glyph commands,
                                     @displaymath as text
    InlineCommandTests.cs            @verb, @acronym, @abbr, @inlinefmt /
                                     @inlinefmtifelse / @inlineraw
    UserIndexTests.cs                @defindex / @defcodeindex, @synindex
                                     merges, @ftable filing
    PrintShapeTests.cs               @setchapternewpage, @lowersections /
                                     @raisesections, @shorttitlepage, @exdent
    MacroExpansionTests.cs           @macro / @rmacro / @linemacro / @alias
                                     argument rules and edge cases
    TexinfoPreprocessorTests.cs      @set / @clear / @value, PredefinedValues,
                                     the two conditional profiles, @include
                                     search paths
    DocumentInvariants.cs            the structural rules every parsed
                                     document obeys (placement, a real
                                     sectioning tree, lookup tables that agree
                                     with it), asserted by the unit tests and
                                     the corpus gate alike

The corpus gate tests in the same folder (LilypondCorpusGateTests,
LilypondParserCorpusGateTests, LilypondEmitterCorpusGateTests) run against the
English LilyPond documentation, which is not in the repository; they show what
a real 110,000-line manual set expects of the renderer.


QUICK REFERENCE CARD
====================

    dotnet add package CodeBrix.Texinfo2Html.MitLicenseForever
    using CodeBrix.Texinfo2Html;

    var r = new TexinfoHtmlRenderer();            //one per thread; reusable
    r.Options.PredefinedValues["VERSION"] = "1.0"; //= @set VERSION 1.0
    r.Options.IncludeSearchPaths.Add("/abs/dir");  //after source dir + parent
    r.Options.ImageSearchPaths.Add("/abs/pics");   //after the include paths
    r.Options.ExtraCss = "...";                    //appended; later rules win
    r.Options.EmitSingleFile = true;               //embed the stylesheet
    r.Options.ConditionalProfile = TexinfoConditionalProfile.Print;  //default
    r.Options.NumberSections = false;
    r.Options.CssFileName = "style.css";           //else <name>.css / texinfo.css
    r.Options.ImageFolderName = "pics";            //else <name>-images / texinfo-images
    r.Options.SnippetRenderer = new MyEngraver();  //ILilypondSnippetRenderer

    TexinfoHtmlResult res = r.GenerateFromFile("manual.texi");
    TexinfoHtmlResult res = r.Generate(sourceText, baseDirectory /*or null*/);

    res.Html  res.BodyHtml  res.Css  res.Title  res.Author
    res.BaseDirectory  res.CssFileName  res.Images  res.Warnings
    string htmlPath = res.WriteToDirectory("out", baseName: null);  //html+css+pictures
    int n = res.CopyImagesTo("out");                                //pictures only
    string doc = res.ToHtmlDocument(myCss);                         //self-contained

    res.Warnings.Messages   //"Category: message (at file:line:col)"
    res.Warnings.Count      //categories: Include Conditional Macro Value
                            //RawBlockSkipped Encoding Syntax UnknownCommand
                            //Reference Emit
    foreach (TexinfoImageReference img in res.Images)
        img.SourcePath  img.RelativePath  img.IsGenerated  img.HasContent  img.GetContent()

    //snippet seam
    LilypondSnippetResult Render(LilypondSnippet s)   //implement this
      s.Kind  s.Source  s.FileName  s.FilePath  s.Options  s.IsInline
      s.BaseDirectory  s.SourceFile  s.LineNumber
      s.Options.Quote .Verbatim .Inline .NoTime .TexiDoc .DocTitle .NoIndent
      s.Options.RaggedRight(bool?) .Fragment(bool?) .Relative(int?) .StaffSize(double?)
      s.Options.LineWidth .Indent .PaperSize .PaperWidth .PaperHeight .All .Unrecognized
      return LilypondSnippetResult.FromContent(bytes, "png")
           | LilypondSnippetResult.FromFile(path)
           | LilypondSnippetResult.FromImages(IEnumerable<LilypondSnippetImage>)
           | LilypondSnippetResult.NotRendered
           | LilypondSnippetResult.Failed("why");
      LilypondSnippetImage.FromFile(path) | LilypondSnippetImage.FromContent(bytes, ".svg")

    Throws only for caller mistakes: ArgumentException (blank path/dir),
    ArgumentNullException (null source), FileNotFoundException (missing file).
    Nothing in a document ever throws - read res.Warnings.

    SVG + PDF: nothing extra on any OS. The PDF stage is fully managed and
    places SVG into the PDF as vector content by default; this package alone
    needs nothing either.

================================================================================
