================================================================================
                         AGENT-README: CodeBrix.Texinfo
                   A Comprehensive Guide for AI Coding Agents
================================================================================


OVERVIEW
--------------------------------------------------------------------------------

CodeBrix.Texinfo is a repository containing two fully managed, cross-platform
.NET libraries that turn GNU Texinfo documentation into nicely-formatted PDF
documents.

CodeBrix.Texinfo2Html reads a Texinfo source file and renders it into HTML and
CSS. The markup it emits is written for PDF generation rather than for the
browser: it stays inside the documented HTML and CSS subset that
CodeBrix.PdfDocCreate.Html2Pdf understands, so the output is ready to be handed
straight to that library.

CodeBrix.Texinfo2Pdf is a convenience library that performs the whole
conversion in one step. It renders the Texinfo source to HTML and CSS with
CodeBrix.Texinfo2Html, then feeds that markup to CodeBrix.PdfDocCreate.Html2Pdf
to produce the finished PDF document.

Both libraries read two input dialects:

    .texi     Standard GNU Texinfo source files.
    .tely     The Texinfo dialect produced by LilyPond and CodeBrix.LilyPort,
              in which Texinfo markup is interleaved with LilyPond music
              snippets.


CURRENT STATUS - READ THIS FIRST
--------------------------------------------------------------------------------

Both libraries are complete and have public APIs. A consumer who wants a PDF
installs CodeBrix.Texinfo2Pdf and calls one method.

CodeBrix.Texinfo2Html renders Texinfo to HTML and CSS end to end. Its whole
pipeline is in place:

  Sources/        source loading, encoding and line-ending handling
  Lexing/         a lossless Texinfo lexer with raw-block capture
  Preprocessing/  @include with search paths, @set/@clear/@value, conditional
                  profiles, raw output blocks, comments, @verbatiminclude, and
                  full @macro/@rmacro/@linemacro/@unmacro/@alias expansion
  Parsing/        the parser and its table of built-in commands
  Model/          the parsed document tree - sections, blocks, inline runs,
                  plus the anchor, index, footnote and settings tables
  Semantics/      section numbering, HTML identifier allocation, heading
                  ranking, table-of-contents construction, footnote placement,
                  float numbering, and index building (merges, sort keys,
                  ordering)
  Snippets/       the lilypond-book option list, and the coordinator that hands
                  music environments to a registered engraver
  Emit/           the HTML emitter, the default print stylesheet, the document
                  builder, image reference resolution and the text conventions
  Diagnostics/    collected warnings, used instead of exceptions throughout

Everything except the types named in CORE API REFERENCE below is internal, and
is exercised directly by the test project through InternalsVisibleTo.

CodeBrix.Texinfo2Pdf owns the hand-off to CodeBrix.PdfDocCreate.Html2Pdf and
nothing else. It parses nothing and emits nothing: it runs Texinfo2Html over the
source, gives the markup and the document's pictures to Html2Pdf, and merges
what both of them had to say. Four public types, one internal staging helper.

What the pair does NOT do, so that no agent documents it as though it did:

  * THE TARGET IS ONE PRINTED DOCUMENT. There is no Info output and no
    split-into-a-website HTML output. @menu is parsed and dropped, and node
    pointers (next, prev, up) are read and ignored, because a PDF is read front
    to back and has no navigation to build.
  * @math and @displaymath are styled text. There is no mathematical
    typesetter here and there is not going to be one.
  * @documentencoding is read and reported, NOT obeyed. Source is UTF-8 (or
    whatever a byte order mark says); see COVERAGE AND SCALE below.
  * A block environment that is NOT preformatted, written inside one that is -
    an @itemize inside an @display - is not rendered as itself. A list or a
    table has no representation inside preformatted text, so it degrades with a
    warning. A PREFORMATTED environment nested in another one does work; see
    NESTED PREFORMATTED BLOCKS below.
  * txidefnamenospace is read as it stood at the END of the document, so a
    document that sets and clears it around one definition gets the setting
    the last @set left. Setting it once, which is how it is meant to be used,
    works.
  * Nothing here engraves music. The seam to do it is defined and wired (see
    MUSIC SNIPPETS below), but this library will not take on a dependency on
    LilyPond, so with no renderer registered a snippet is its source text.

Keep new source files organized into sub-folders and matching sub-namespaces
(see ARCHITECTURE below); only the entry-point types belong at a project root.
Update this file as the public API grows. Document what is true, never what is
planned.


INSTALLATION
--------------------------------------------------------------------------------

NuGet packages:  CodeBrix.Texinfo2Html.MitLicenseForever
                 CodeBrix.Texinfo2Pdf.MitLicenseForever

    dotnet add package CodeBrix.Texinfo2Html.MitLicenseForever
    dotnet add package CodeBrix.Texinfo2Pdf.MitLicenseForever

Install CodeBrix.Texinfo2Pdf when you want a PDF; it depends on
CodeBrix.Texinfo2Html and on CodeBrix.PdfDocCreate.Html2Pdf, so it brings the
whole conversion chain with it. Install CodeBrix.Texinfo2Html on its own when
you want the intermediate HTML and CSS, or when you want to post-process the
markup before it is rendered to PDF.

Note that the NuGet package ids carry the ".MitLicenseForever" suffix but the
assemblies and the namespaces do NOT - they are simply CodeBrix.Texinfo2Html
and CodeBrix.Texinfo2Pdf. The suffix is a CodeBrix family convention that
records the license the packages will always be published under.

Target framework: .NET 10.0 or higher. Both libraries target net10.0 only.


KEY NAMESPACES
--------------------------------------------------------------------------------

    using CodeBrix.Texinfo2Html;    //Texinfo source -> HTML and CSS
    using CodeBrix.Texinfo2Pdf;     //Texinfo source -> PDF, in one step

Every public type of each library sits in that library's own root namespace; the
sub-namespaces underneath them (Sources, Lexing, Preprocessing, Parsing, Model,
Semantics, Emit, Diagnostics in Texinfo2Html, Rendering in Texinfo2Pdf) are all
internal. There is no separate CodeBrix.Texinfo namespace and no project named
CodeBrix.Texinfo - that name belongs to the repository and to the solution file
only.

Using CodeBrix.Texinfo2Pdf does not mean giving up the HTML stage: its
GenerateHtml methods hand back the very same TexinfoHtmlResult, so a consumer
who installs the PDF package still has the whole intermediate available and
does not need the Texinfo2Html package as well.


WHAT IT DOES WITH A DOCUMENT
--------------------------------------------------------------------------------

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
    in a fixed-width font. Entries are grouped under letter headings, set
    in a fixed-width font for every predefined index except the concept one,
    and each
    line links to a marker left where the entry was written AND names the
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
    follows. Do not "fix" this into a table: a definition sits inside lists,
    quotations and other definitions, and a table there is a layout trap.

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
    Do not "fix" this into a second <pre>: no browser accepts one and Html2Pdf
    is not being asked to. The inner block keeps its OWN text conventions, so an
    @example inside a @display still has its dashes left alone while the prose
    around it is converted - which is why it stays a node of its own rather than
    being flattened into text. Indentation is written when a line turns out to
    have content, never on the line break that ended the one before, so blank
    lines stay blank and nothing trails. A paragraph directive written inside a
    preformatted block (@noindent, @indent) is dropped, line and all.

  * A LINE MACRO READS A WHOLE LINE. @linemacro defines a macro that is called
    as a line command, and its argument rules are unlike every other invocation
    form in Texinfo: arguments are separated by SPACES rather than commas, a
    pair of braces enclosing an argument is removed, an empty argument has to be
    written as {}, and the last argument takes the whole rest of the line so it
    may hold spaces unbraced. A brace after the macro name therefore opens the
    first ARGUMENT and is not a brace-form argument list. Do not "improve" this
    into the comma splitting the other forms use - it is a different rule, not
    an oversight. A line whose last character is a lone @ continues onto the
    next one, and that @ and its newline stay inside the argument, because what
    the expansion has to produce is a valid definition line. The reason the
    command exists is to let a manual define its own definition commands on top
    of @defline and @deftypeline, so it earns its keep only alongside those.


COVERAGE AND SCALE - WHAT TO EXPECT OF A REAL MANUAL
--------------------------------------------------------------------------------

There is no list of supported commands to check a document against, because the
answer is not a list: what a document uses that these libraries do not implement
becomes a WARNING and the nearest readable degradation, never an exception and
never a lost document. So the useful question is not "is @foo supported" but
"how many warnings does my manual produce, and do I mind them". Render it and
read result.Warnings - that is the intended way to find out.

What the libraries are actually run against, every time the test suite runs:

  * THE ENGLISH LILYPOND DOCUMENTATION SET - eight manuals, about 110,000 lines
    across 123 files, the largest of them the 51,000-line notation reference.
    All eight parse and render to PDF, and every internal link in the output has
    a destination in the same document. This corpus leans hard on @macro (its own
    two macro files define 158 of them), on the .tely music environments, and on
    accented and Cyrillic text.
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

Scale, so nobody has to guess:

  * The notation reference - 51,000 lines in, 965 pages out - takes on the order
    of SIX SECONDS end to end on a developer laptop. Reading and parsing the
    Texinfo is a fraction of a second of that; the time is the typesetting, and
    it is Html2Pdf's, not this library's.
  * Cost is linear in document size. There is a test that renders synthetic
    manuals of 500 and 2,000 sections purely to fail if anything ever goes
    quadratic, because a corpus test would only report that as "slow today".
  * A reused renderer accumulates nothing between documents; reading the same
    manual twice through one renderer costs the same both times.

The shape that follows from this: a manual of any size is comfortable in a build
step or an offline job. Rendering a 965-page manual inside a web request is not
what any of this is for.


MUSIC SNIPPETS (the .tely dialect)
--------------------------------------------------------------------------------

@lilypond (block form and brace form), @lilypondfile and @musicxmlfile are
parsed, their bracketed options are read into named properties, and the whole
snippet is offered to ILilypondSnippetRenderer if the caller registered one on
Options.SnippetRenderer.

WITH NO RENDERER - the default - the snippet is shown as its source text in a
preformatted block and ONE warning records how many there were. That is not a
placeholder to be removed later: engraving music means running LilyPond, and
this library will not take on that dependency.

WITH A RENDERER, the pictures it returns are placed and are registered in
result.Images, so they travel with the document exactly as @image pictures do -
including pictures handed over as bytes, which WriteToDirectory writes out.

    var renderer = new TexinfoHtmlRenderer();
    renderer.Options.SnippetRenderer = new MyEngraver();

Two option names decide what the DOCUMENT does; every other option is the
engraver's business and is simply passed along:

    verbatim   show the source as well as the engraving. The source is written
               ABOVE the picture - input first, then what it produces.
    quote      indent the snippet. Written as an inline style, NOT as a
               container: a bordered container is laid out by Html2Pdf as one
               box, and that is what silently swallowed multitables inside
               @quotation until it was written this way. Do not "improve" this
               into a <div>.

The option vocabulary is the one MEASURED across the whole English LilyPond
documentation set, not one invented from the syntax: quote, verbatim, inline,
notime, texidoc, doctitle, noindent, ragged-right, noragged-right, fragment,
nofragment, relative[=N], staffsize=, line-width=, indent=, papersize=,
paper-width=, paper-height=. Anything else is kept as written in Options.All,
listed in Options.Unrecognized, and reported once for the document - a renderer
that knows an option this library does not can still act on it. Dimensions are
kept as strings ("3\cm", "6\in") because they are LilyPond's own units and mean
nothing outside an engraver; do not convert them.

Three things about this seam that are easy to get wrong:

  * AN IDENTICAL SNIPPET IS ENGRAVED ONCE. The cache key is everything the
    renderer is given, so reuse can never change what is shown. The notation
    reference holds over 1,600 snippets and repeats many of them.
  * A RENDERER THAT THROWS IS CAUGHT and turned into the failure it should have
    returned. A document must not be lost to a consumer's exception.
  * @lilypondfile WITH verbatim READS THE FILE - the one place this library
    opens a music file rather than only naming it - and starts at the file's
    "% begin verbatim" marker when it has one. Every one of the 389 snippet
    files in the LilyPond documentation carries that marker at the end of its
    \header block, which is exactly the boilerplate it exists to skip. A file
    that is missing is only reported when a renderer was registered; with none,
    the document never needed it.


IMAGES
--------------------------------------------------------------------------------

An @image reference names a file with no directory and usually no extension, so
the file is searched for and its extension probed (.png .jpg .jpeg .gif .bmp
.svg, plus any extension the command declares). Do NOT add .pdf to that probe:
a manual that keeps pdf/NAME variants for its TeX branch would then hand
Html2Pdf a file it cannot decode.

What the markup writes is NOT where the file was found. It is a path relative
to the document itself - <ImageFolderName>/<file> - so the generated document
is complete once its pictures are beside it:

    WriteToDirectory(dir)   writes the markup and the stylesheet AND copies
                            every picture into dir, which is everything the
                            document needs.
    CopyImagesTo(dir)       does only the copying, for a caller that renders
                            result.Html from memory. Pass the same dir to
                            Html2Pdf as the base directory.
    result.Images           the whole mapping: SourcePath -> RelativePath.

Two pictures with the same file name from different directories are numbered
apart. A picture that cannot be found becomes its alternate text in grey
italic, plus one warning.


CORE API REFERENCE - CodeBrix.Texinfo2Html
--------------------------------------------------------------------------------

Twelve public types, all in the CodeBrix.Texinfo2Html namespace: six for
rendering a document, and six making up the music-snippet seam.

--- TexinfoHtmlRenderer ---

    var renderer = new TexinfoHtmlRenderer();

    TexinfoHtmlResult GenerateFromFile(string texinfoFilePath)
    TexinfoHtmlResult Generate(string texinfoSource, string baseDirectory = null)
    TexinfoHtmlOptions Options { get; }

One renderer can be reused for many documents; set Options before calling, and
note that it is NOT safe across threads - give each thread its own renderer.
Rendering never throws over the contents of a document - anything unsupported,
malformed or missing becomes a warning in the result plus the nearest readable
degradation. Exceptions are reserved for the caller's own mistakes:
ArgumentException for a blank path, FileNotFoundException for a source file
that is not there.

GenerateFromFile seeds the search paths with the source file's directory AND
that directory's parent, in that order, which is what lets a manual written as
a tree of @include files render from its top-level source, and what lets
@image{pictures/foo} resolve from a sibling directory.

--- TexinfoHtmlOptions ---

    bool                       EmitSingleFile          (false)
    TexinfoConditionalProfile  ConditionalProfile      (Print)
    List<string>               IncludeSearchPaths      (empty)
    List<string>               ImageSearchPaths        (empty)
    Dictionary<string,string>  PredefinedValues        (empty)
    bool                       NumberSections          (true)
    string                     ExtraCss                ("")
    string                     CssFileName             ("" - derived)
    string                     ImageFolderName         ("" - derived)
    ILilypondSnippetRenderer   SnippetRenderer         (null)

PredefinedValues acts as though the source opened with @set name value, which
is how to supply the version and date strings a manual's build normally
generates into an included file. ExtraCss is appended after the built-in
stylesheet, so a repeated rule of equal specificity wins. CssFileName and
ImageFolderName are derived from the source file's name when left empty
(manual.css and manual-images/), or are texinfo.css and texinfo-images/ for a
document rendered from a string.

--- TexinfoHtmlResult ---

    string Html            //the complete document
    string BodyHtml        //the generated markup on its own
    string Css             //always separate, even when it was embedded
    string Title           //from @settitle
    string Author          //from the title page's FIRST @author
    string BaseDirectory
    string CssFileName
    IReadOnlyList<TexinfoImageReference> Images
    TexinfoRenderWarnings Warnings

    string ToHtmlDocument(string replacementCss)
    string WriteToDirectory(string directory, string baseName = null)
    int    CopyImagesTo(string directory)

WriteToDirectory creates the directory if needed, writes <baseName>.html,
writes the stylesheet beside it under CssFileName unless EmitSingleFile was
set, and copies the document's pictures into it. It returns the full path of
the HTML file.

--- TexinfoImageReference ---

    string SourcePath      //where the file was found at render time; "" for a
                           //picture a snippet renderer handed over as bytes
    string RelativePath    //what the markup points at, forward slashes
    bool   IsGenerated     //engraved from a snippet rather than named by @image
    bool   HasContent      //held in memory; there is no file to copy from
    byte[] GetContent()    //a copy of those bytes, or an empty array

--- TexinfoRenderWarnings ---

    IReadOnlyList<string> Messages
    int Count

Every message is prefixed with its category, which is what a test filters on:
Include, Conditional, Macro, Value, RawBlockSkipped, Encoding, Syntax,
UnknownCommand, Reference or Emit.

--- ILilypondSnippetRenderer and its five companions ---

    LilypondSnippetResult Render(LilypondSnippet snippet)

    LilypondSnippet         Kind, Source, FileName, FilePath, Options,
                            IsInline, BaseDirectory, SourceFile, LineNumber
    LilypondSnippetKind     Music | LilypondFile | MusicXmlFile
    LilypondSnippetOptions  the named options, plus All and Unrecognized
    LilypondSnippetImage    FromFile(path) | FromContent(bytes, extension)
    LilypondSnippetResult   FromFile / FromContent / FromImages / Failed /
                            NotRendered; Images, ErrorMessage, IsRendered,
                            IsFailure

IsInline says where the snippet SITS (inside a paragraph); Options.Inline is
the option asking for a small engraving. They are different questions and a
renderer usually wants both. FilePath is empty when the named file was not
found, and a renderer handed an empty path should decline rather than guess.

--- TexinfoConditionalProfile ---

    Print   //@iftex and every @ifnot... branch; the right one for PDF output
    Html    //@ifhtml on, @ifnothtml off

Print deliberately reads BOTH @iftex and @ifnottex, because real manuals put
document structure - most often the @node Top and @top pair the whole document
hangs from - in the @ifnottex branch. The cost is that a document writing the
same visible content into both branches contributes it twice.

--- Driving Html2Pdf by hand ---

Reach for CodeBrix.Texinfo2Pdf instead of doing this; it is written below and it
gets the picture staging right. But a consumer who has the Texinfo2Html package
alone and wants a PDF does it like this:

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


CORE API REFERENCE - CodeBrix.Texinfo2Pdf
--------------------------------------------------------------------------------

Four public types, all in the CodeBrix.Texinfo2Pdf namespace.

--- TexinfoPdfRenderer ---

    var renderer = new TexinfoPdfRenderer();

    // workflow one - source in, PDF out
    TexinfoPdfResult RenderFile(string texinfoFilePath, string outputPdfPath = null)
    TexinfoPdfResult RenderTexinfo(string texinfoSource, string outputPdfPath,
                                   string baseDirectory = null)
    TexinfoPdfResult RenderFileToBytes(string texinfoFilePath)
    TexinfoPdfResult RenderTexinfoToBytes(string texinfoSource, string baseDirectory = null)

    // workflow two, step A - source in, markup out
    TexinfoHtmlResult GenerateHtmlFromFile(string texinfoFilePath)
    TexinfoHtmlResult GenerateHtml(string texinfoSource, string baseDirectory = null)

    // workflow two, step B - markup back in, PDF out
    TexinfoPdfResult RenderHtml(TexinfoHtmlResult htmlResult, string outputPdfPath,
                                string replacementCss = null)
    TexinfoPdfResult RenderHtmlToBytes(TexinfoHtmlResult htmlResult, string replacementCss = null)
    TexinfoPdfResult RenderHtmlFile(string htmlFilePath, string outputPdfPath)
    TexinfoPdfResult RenderHtmlDocument(string html, string outputPdfPath,
                                        string baseDirectory = null)

    TexinfoPdfOptions Options { get; }

RenderFile with no output path writes the PDF beside the source under the same
name. Every method that takes an output path CREATES the directory it names, so
"out/manual.pdf" works without the caller making "out" first.

Nothing about a document's contents throws; both stages degrade and report.
Exceptions are the caller's own mistakes: ArgumentException for a blank path,
ArgumentNullException for a null source or result, FileNotFoundException for a
file that is not there.

One renderer serves many documents. It is not safe across threads, and neither
are the two renderers it owns.

--- TexinfoPdfOptions ---

    TexinfoHtmlOptions  Texinfo    //how the source is read
    HtmlRenderOptions   Html       //what the PDF looks like

NEITHER IS A COPY. They are the live options objects of the two renderers
underneath, so every setting either library has - including any it gains later -
is reachable without a second package reference and without anything here having
to be kept in step. Do not "improve" this into a copy: the drift is the bug it
was written to avoid.

Two defaults differ from bare Html2Pdf, because a printed manual wants them:

    Html.HeaderText = "{title}"
    Html.FooterText = "{page} / {pages}"

Set either to an empty string to be rid of it. Html.DocumentTitle and
Html.DocumentAuthor are filled in from the document's own @settitle and @author
WHEN THE CALLER LEFT THEM EMPTY, and are put back to what the caller had after
every render - which is what stops one manual's title following a reused
renderer to the next.

--- TexinfoPdfResult ---

    string             OutputFilePath  //"" for a render that produced bytes
    byte[]             PdfBytes        //null for a render that wrote a file
    int                PageCount
    string             Title
    TexinfoHtmlResult  Intermediate    //null when the caller supplied the markup
    TexinfoPdfWarnings Warnings

Intermediate is the whole HTML/CSS result the PDF was made from, so a one-shot
conversion still gives access to the markup, the stylesheet and the picture
list without running the source twice.

--- TexinfoPdfWarnings ---

    IReadOnlyList<string> Messages         //both stages, each tagged
    IReadOnlyList<string> TexinfoMessages  //untagged, as Texinfo2Html wrote them
    IReadOnlyList<string> PdfMessages      //untagged, as Html2Pdf wrote them
    int                   Count
    const string          TexinfoStageTag = "[texinfo]"
    const string          PdfStageTag     = "[pdf]"

A message means a different thing depending on which stage said it - the Texinfo
stage is talking about the source, the PDF stage about the markup or the fonts -
so filter on the split lists rather than pattern-matching the merged one. The
Texinfo stage ran first and is listed first.

--- The two workflows ---

    //(a) one shot
    var result = new TexinfoPdfRenderer().RenderFile("manual.texi", "out/manual.pdf");
    Console.WriteLine($"{result.PageCount} pages, {result.Warnings.Count} warnings");

    //(b) restyle the intermediate first
    var renderer = new TexinfoPdfRenderer();
    var html = renderer.GenerateHtmlFromFile("manual.texi");
    var myCss = html.Css.Replace("#111111", "#000033");   //or replace wholesale
    renderer.RenderHtml(html, "out/manual.pdf", myCss);

In workflow (b) the pictures need no handling at all: RenderHtml stages them in
a temporary directory for the length of the render and sweeps it up afterwards,
so what lands in "out" is one PDF and nothing else. That is the whole reason
this library exists rather than a paragraph of instructions.

A caller who would rather edit the files on disk writes the pair out, edits it,
and comes back in through RenderHtmlFile:

    var html = renderer.GenerateHtmlFromFile("manual.texi");
    var path = html.WriteToDirectory("work");             //manual.html + manual.css
    //...edit work/manual.html and work/manual.css by hand...
    renderer.RenderHtmlFile(path, "out/manual.pdf");


REPOSITORY LAYOUT
--------------------------------------------------------------------------------

    CodeBrix.Texinfo.slnx                     Solution - four projects

    src/CodeBrix.Texinfo2Html/                Renders Texinfo to HTML and CSS
    src/CodeBrix.Texinfo2Pdf/                 Renders Texinfo to PDF

    tests/CodeBrix.Texinfo2Html.Tests/        xUnit v3 tests for Texinfo2Html
    tests/CodeBrix.Texinfo2Pdf.Tests/         xUnit v3 tests for Texinfo2Pdf

Project relationships:

    CodeBrix.Texinfo2Pdf
        -> ProjectReference   CodeBrix.Texinfo2Html
        -> PackageReference   CodeBrix.PdfDocCreate.Html2Pdf.MitLicenseForever

The ProjectReference is what is used for local build and debug. At pack time
NuGet automatically turns it into a package dependency on
CodeBrix.Texinfo2Html.MitLicenseForever, using that project's PackageId and
computed version - so do not add a hand-written PackageReference to
CodeBrix.Texinfo2Html.MitLicenseForever alongside it.

CodeBrix.Texinfo2Html has no package or project dependencies at all beyond
.NET itself, and it should stay that way: everything it needs to emit HTML and
CSS is in the framework, and keeping it dependency-free is what lets a consumer
take the HTML/CSS path without pulling in the PDF stack.


ARCHITECTURE
--------------------------------------------------------------------------------

The split between the two libraries, which is a rule and not just a description:

  CodeBrix.Texinfo2Html owns everything about the Texinfo format - lexing the
  @-commands, resolving cross references and node structure, handling the .tely
  dialect's LilyPond blocks, and emitting the HTML and CSS. It knows nothing
  about PDF.

  CodeBrix.Texinfo2Pdf owns the hand-off to CodeBrix.PdfDocCreate.Html2Pdf and
  the options that govern the finished document. It knows nothing about the
  Texinfo format beyond passing the source through to CodeBrix.Texinfo2Html.
  It has no parsing, no emission and no markup of its own, and it must not
  acquire any: anything it would need to know about a document belongs on
  TexinfoHtmlResult instead.

Two things inside CodeBrix.Texinfo2Pdf are less obvious than they look:

  * IT ALWAYS HANDS HTML2PDF A SELF-CONTAINED DOCUMENT. The generated markup
    links to a stylesheet beside it, which would mean writing that stylesheet
    somewhere for the render to find. Embedding it instead - ToHtmlDocument
    with the result's own Css - says exactly the same thing to Html2Pdf and
    leaves only the pictures needing a home. Do not "fix" this into writing the
    pair out.
  * THE PICTURES GO TO A TEMPORARY DIRECTORY, NOT THE OUTPUT ONE. The markup
    points at them relative to wherever the document is put, so a render from
    memory needs somewhere for those paths to lead - and a caller who asked for
    one PDF must not get a folder of pictures next to it. Rendering/
    ImageStagingArea creates the directory only when the document has pictures
    at all, and removes it afterwards.

The HTML and CSS that CodeBrix.Texinfo2Html emits is a contract with
CodeBrix.PdfDocCreate.Html2Pdf, not general-purpose web markup. Html2Pdf applies
a documented subset of CSS - inline style attributes, style blocks and linked
local stylesheets, with real selector matching, cascade, specificity and
inheritance - and it renders all text through the CodeBrix.Platform.Fonts
packages so output is identical on every operating system. Stay inside that
subset. When you are unsure whether a construct is supported, check the
Html2Pdf AGENT-README rather than guessing; markup that a browser tolerates is
not automatically markup that Html2Pdf renders.

Inside CodeBrix.Texinfo2Html the stages run in one order and each owns one
sub-folder and matching sub-namespace: Sources -> Lexing -> Preprocessing ->
Parsing (into Model) -> Semantics -> Emit, with Diagnostics collecting warnings
throughout. Keep that shape. The parser records only what the source said; the
semantic pass works out what it meant across the whole document (numbering,
identifiers, the contents); the emitter writes markup and decides nothing about
meaning. Only the five public entry-point types sit at the project root.


CODING CONVENTIONS (CodeBrix family)
--------------------------------------------------------------------------------

These are family-wide rules. They are not negotiable per-file, and the build is
configured so that violating most of them produces a warning.

  * Target framework is net10.0 only. No multi-targeting.

  * Nullable reference types are OFF. Never write a '?' on a reference type -
    no string?, no MyClass?, no object?. Value-type nullables (int?, bool?,
    DateOnly?, MyEnum?) are fine, because those are Nullable<T>. Never use the
    null-forgiveness '!' operator, and never add '#nullable enable'.

  * ImplicitUsings are OFF and there are no global usings. Every file declares
    its own using directives.

  * Namespaces are file-scoped ('namespace CodeBrix.Texinfo2Html.Models;').
    Never block-scoped.

  * File layout is always: usings first, then the namespace line, then the
    type. No leading blank line at the top of the file, no using directives
    below the namespace line, no blank lines inside the using block. System.*
    usings come first, then the rest, alphabetical within each group.

  * Warnings are fixed at source. Never add <NoWarn>, <WarningLevel>0</>,
    #pragma warning disable, or any other project-level or file-level
    suppression to make a warning go away.

  * <GenerateDocumentationFile> is true on both libraries, so CS1591 fires for
    any undocumented public member. Every public type and every public,
    protected or protected internal member needs an XML doc comment, including
    every enum value. Write summaries that say something the identifier does
    not already say.

  * Tests are xUnit v3 with SilverAssertions. Prefer the fluent form -
    'actual.Should().Be(expected)' over 'Assert.Equal(expected, actual)'.

  * The copyright string is the fixed literal
    'Copyright (c) 2026 Jeremy Ellis and contributors'.

  * Do not hardcode a literal <Version> in a packable csproj. Both library
    csproj files carry the family's canonical date-stamped version block, which
    computes 1.<years-since-2026>.<day-of-year>.<minute-of-day> from UtcNow at
    build time. Leave that block alone.


TESTING
--------------------------------------------------------------------------------

Each library has exactly one test project, named after it with a .Tests suffix,
and each library ships an InternalsVisibleTo.cs granting that test project
access to its internals - so internal helpers can and should be tested directly
by name.

Test conventions:

  * A test file that covers one class is named <ClassUnderTest>Tests.cs and
    holds 'public class <ClassUnderTest>Tests'. Files that exercise several
    classes together, or that hold test helpers, get a descriptive name
    instead.

  * Method-specific tests are named '<MemberName>_<snake_case_description>'.
    Tests that are not about one member are pure snake_case.

  * A multi-statement test body carries //Arrange, //Act and //Assert comments.
    A single-statement test body is expression-bodied instead.

  * Any call inside a test that accepts a CancellationToken must be passed
    TestContext.Current.CancellationToken, or xUnit1051 fires.

Two suites need the English LilyPond documentation, which is GFDL-licensed and
so is never committed here. They read it from ~/GitHome/lilypond/Documentation
and skip cleanly when it is absent:

  LilypondEmitterCorpusGateTests   (CodeBrix.Texinfo2Html.Tests)
      every manual renders to markup with only the expected warnings; EVERY
      internal link in the output - contents, cross reference or index line -
      has a destination in the same document; and the manuals that print an
      index print one of the expected size.

  TexinfoToPdfGateTests            (CodeBrix.Texinfo2Pdf.Tests)
      the end-to-end gate: Texinfo -> HTML/CSS -> PDF, with Html2Pdf reporting
      nothing but font-coverage messages. This is the test that proves the two
      libraries agree on the markup subset; nothing inside
      CodeBrix.Texinfo2Html alone can show that. It runs THROUGH THE SHIPPED
      CodeBrix.Texinfo2Pdf API rather than through a chain the test assembles,
      so it gates what a consumer actually gets - keep it that way. It also
      holds the glyph coverage check: no character in a script the
      CodeBrix.Platform.Fonts packages cover may be dropped from a PDF. What the
      corpus does drop is the two musical accidental signs and a Hebrew lyric
      quoted inside a snippet - no text font carries either, and CodeBrix never
      falls back to a system font. It leaves the PDFs it built in
      <temp>/codebrix-texinfo-gate so they can be looked at afterwards.

  NotationStressTests              (CodeBrix.Texinfo2Pdf.Tests)
      scale. The notation reference is the largest thing either library will be
      asked to render, and this renders it end to end inside a time budget, then
      reads it twice over to show a reused renderer accumulates nothing. Its
      third test needs NO corpus: it renders synthetic manuals of 500 and 2000
      sections - each with a node, an index entry and a cross reference, the
      three things looked up across the whole document - and fails if four times
      the document costs anything like sixteen times the time. That is the test
      that would actually catch a structure going quadratic; the corpus test
      would only ever report it as "slow today". The time bounds are loose on
      purpose and are not to be tightened into something a busy machine trips.

The general-Texinfo commands - the definition family, floats, accents, @verb,
@acronym, the @inline... conditionals, the user-defined indices and the
print-shape commands - are covered by one test file per feature area
(DefinitionCommandTests, FloatTests, AccentCommandTests, InlineCommandTests,
UserIndexTests, PrintShapeTests), each carrying an original fixture document
written for it. The LilyPond corpus is no help there BY DEFINITION: those are
precisely the commands a music manual does not use, so the fixtures are the
coverage. TexinfoToPdfGateTests holds the one document that uses all of them at
once and renders it to a real PDF, which is what says the markup they produce is
inside the Html2Pdf subset.

SnippetToPdfGateTests (CodeBrix.Texinfo2Pdf.Tests) needs no corpus. It registers
an engraver and proves that what a renderer hands back becomes a picture in a
real PDF. The picture is BUILT by the test rather than committed - TestPng.Build
writes a valid PNG from first principles - so the repository carries no binary
fixture and so the claim being tested is that Html2Pdf really decodes it.

TexinfoPdfRendererTests (CodeBrix.Texinfo2Pdf.Tests) covers the composition
library's own API from source written for it: both workflows, the options
reaching both stages, the metadata fill-in and its undo, the stage-tagged
warning merge, and the caller mistakes that are meant to throw. Two of its tests
work from a deliberately LONG document, because in a short manual every page
break is a chapter starting and no change to the stylesheet can move the page
count - which is what makes a stylesheet assertion look broken when it is not.

Fixtures committed to this repository must be original work written for the
test, or come from an explicitly MIT, CC0 or public-domain source listed in
THIRD-PARTY-NOTICES.txt. Nothing GFDL, nothing GPL.

Run the whole suite from the repository root:

    dotnet test CodeBrix.Texinfo.slnx


================================================================================
