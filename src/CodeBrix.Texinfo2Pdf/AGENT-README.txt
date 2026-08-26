================================================================================
AGENT-README: CodeBrix.Texinfo2Pdf
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Texinfo2Pdf.MitLicenseForever NuGet package
================================================================================


OVERVIEW
========

CodeBrix.Texinfo2Pdf is a fully managed, cross-platform .NET convenience
library that turns a GNU Texinfo source file into a finished, nicely formatted
PDF in one call. It renders the source to HTML and CSS with
CodeBrix.Texinfo2Html, then feeds that markup to CodeBrix.PdfDocCreate.Html2Pdf
to produce the PDF - and it merges what both stages had to say into one warning
list.

It targets .NET 10 or later.

It reads two input dialects:

    .texi     Standard GNU Texinfo source files.
    .tely     The Texinfo dialect produced by LilyPond and CodeBrix.LilyPort,
              in which Texinfo markup is interleaved with LilyPond music
              snippets.

This library owns the hand-off and nothing else. It parses nothing and emits
nothing: it runs Texinfo2Html over the source, gives the markup and the
document's pictures to Html2Pdf, and stages the pictures in a temporary
directory for the length of the render so that what lands beside the PDF is
nothing at all. Five public types, one namespace. Everything about the Texinfo
format itself - what is rendered how, which commands degrade with a warning,
music snippets, images - is documented in the CodeBrix.Texinfo2Html
AGENT-README and applies here unchanged, because the same renderer is doing
the reading.

Provenance: an original implementation; no third-party source is incorporated.


################################################################################
## NO NATIVE DEPENDENCIES - AND SVG LANDS IN THE PDF AS VECTOR CONTENT
################################################################################

Every part of this conversion is managed code, on every operating system.
Texinfo2Html emits HTML and CSS and rasterizes nothing; the PDF stage
underneath - CodeBrix.PdfDocCreate.Html2Pdf - draws SVG with
CodeBrix.Imaging.Drawing.NoSkia. There is NO SkiaSharp, no native library, no
GPU, no window system and no system font anywhere in the chain, so Windows,
macOS and Linux need NOTHING beyond the NuGet packages themselves: no
apt/brew/msi step, no runtime identifier, no native-assets package. Do not add
one - not here, not in a consuming application - and do not report the absence
of one as a missing dependency.

  UPGRADING: this package pins CodeBrix.PdfDocCreate.Html2Pdf 1.0.238.580 (the
  version in its csproj). Earlier releases of that stage rasterized SVG
  through a Skia-based engine, and asked Linux applications to reference
  SkiaSharp.NativeAssets.Linux or ...Linux.NoDependencies themselves. That
  requirement is GONE. An application that referenced one of those packages
  ONLY for this conversion can drop the reference; one that uses it for its
  own reasons can keep it, and nothing here changes. The warning code
  "image.svg.nativemissing" is RETIRED and can no longer be raised - delete
  any code that pattern-matches it.

HOW SVG REACHES THE PAGE. By default the picture's drawing commands are written
into the page as PDF operators: paths, fills, strokes, dashes, clips,
transforms, gradients as PDF shading patterns, group opacity as a PDF
transparency group, and SVG <text> as REAL PDF text in the embedded face. The
picture stays sharp at any zoom, its text stays selectable and searchable, and
NO image XObject is added to the file.

The mode is the PDF stage's own option, reachable through this package without
a second package reference:

    using CodeBrix.PdfDocCreate.Html2Pdf;   //only to NAME SvgPlacementMode

    var renderer = new TexinfoPdfRenderer();
    renderer.Options.Html.SvgPlacement = SvgPlacementMode.Raster;  //default Vector
    renderer.Options.Html.SvgRasterScale = 3.0;                    //default 2.0

    public enum SvgPlacementMode { Vector = 0, Raster = 1 }

  Vector (the default)  vector content, as described above.
  Raster                the whole picture is rasterized to a transparent PNG in
                        managed code and embedded as a bitmap - the placement
                        every release before the vector route used.

SvgRasterScale is relative to the SVG's natural CSS-pixel size (2.0 is about
192 DPI), is clamped to 0.25-8.0, and never changes the PLACED size of the
picture. In Raster mode it sets the whole picture's density. In VECTOR mode it
applies ONLY to a part PDF cannot express - an image filter such as a blur,
Porter-Duff compositing, a difference clip, a repeating or reflecting gradient,
a gradient whose stops differ in alpha, or a pattern fill. Such a part is
rasterized on its own, the rest of the picture stays vector, and the fallback
is reported as a PDF-stage warning with the code "image.svg.rasterized". A
picture that stays entirely vector is untouched by the setting.

Bitmap pictures (PNG, JPEG, WebP and the rest) are placed as bitmaps, exactly
as before, on every operating system.

################################################################################


INSTALLATION
============

    dotnet add package CodeBrix.Texinfo2Pdf.MitLicenseForever

PackageId:      CodeBrix.Texinfo2Pdf.MitLicenseForever
Assembly:       CodeBrix.Texinfo2Pdf
Namespace:      CodeBrix.Texinfo2Pdf
License:        MIT
Dependencies:   CodeBrix.Texinfo2Html.MitLicenseForever
                CodeBrix.PdfDocCreate.Html2Pdf.MitLicenseForever
                (and, through Html2Pdf, its own dependencies including the
                CodeBrix.Platform.Fonts packages the PDF is set in: Roboto,
                Merriweather, RobotoMono and NotoMusic)
Requirements:   none, on any operating system. No native-assets package, no
                runtime identifier, no system font, no apt/brew/msi step
                (see the notice above).

Installing this one package brings the whole conversion chain with it. You do
not need a separate reference to CodeBrix.Texinfo2Html.MitLicenseForever: its
types (TexinfoHtmlResult, TexinfoHtmlOptions, ILilypondSnippetRenderer and the
rest) flow through transitively and are part of this package's API. You do not
need a separate reference to CodeBrix.PdfDocCreate.Html2Pdf.MitLicenseForever
either, unless you want to name its types (HtmlRenderOptions, RenderWarning) in
your own declarations - and even then it is already on the dependency graph.

The package id carries the ".MitLicenseForever" suffix but the assembly and the
namespace do NOT - they are simply CodeBrix.Texinfo2Pdf. The suffix is a
CodeBrix family convention that records the license the package will always be
published under.


KEY NAMESPACES / USINGS
=======================

    using CodeBrix.Texinfo2Pdf;             //TexinfoPdfRenderer and its four companions
    using CodeBrix.Texinfo2Html;            //TexinfoHtmlResult, TexinfoHtmlOptions,
                                            //ILilypondSnippetRenderer ... (flows through)
    using CodeBrix.PdfDocCreate.Html2Pdf;   //HtmlRenderOptions, RenderWarning,
                                            //RenderWarningCategory (only when you
                                            //declare variables of those types)

The five public types, all in CodeBrix.Texinfo2Pdf:

    TexinfoPdfRenderer     the entry point - ten methods, two workflows
    TexinfoPdfOptions      Options.Texinfo and Options.Html, both LIVE objects
    TexinfoPdfResult       what a render returns
    TexinfoPdfWarnings     both stages' warnings, tagged, split and structured
    TexinfoPdfFonts        static font registration for the PDF stage

The sub-namespace CodeBrix.Texinfo2Pdf.Rendering is internal. Using this
package does not mean giving up the HTML stage: GenerateHtml and
GenerateHtmlFromFile hand back the very same TexinfoHtmlResult that
CodeBrix.Texinfo2Html produces, so the whole intermediate is available.


CORE API REFERENCE
==================

TexinfoPdfRenderer
------------------

    var renderer = new TexinfoPdfRenderer();

    TexinfoPdfOptions Options { get; }

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

RenderFile with no output path writes the PDF beside the source under the same
name with a .pdf extension. Every method that takes an output path CREATES the
directory it names, so "out/manual.pdf" works without the caller making "out"
first. The source file's directory and that directory's parent become the first
places @include and @image look, exactly as in CodeBrix.Texinfo2Html.

The ...ToBytes methods return the PDF in TexinfoPdfResult.PdfBytes and write
nothing to disk - for a caller sending the document somewhere rather than
storing it.

baseDirectory (RenderTexinfo, RenderTexinfoToBytes, GenerateHtml) is what
@include and @image references in in-memory source resolve against; pass null
when the source needs no files of its own.

RenderHtml and RenderHtmlToBytes take a TexinfoHtmlResult back - altered
stylesheet or not - and finish the job. The document's pictures travel with
it: they are staged in a temporary directory for the length of the render and
swept up afterwards, so nothing has to be written out first and nothing lands
beside the PDF. replacementCss is used in place of the generated stylesheet
(take htmlResult.Css, change it or replace it outright, and pass it here); null
means the generated stylesheet. The document is always handed to the PDF stage
as one self-contained HTML file with the stylesheet embedded.

RenderHtmlFile is the way back in for a caller who wrote the document out with
TexinfoHtmlResult.WriteToDirectory and edited the files by hand: a linked
stylesheet and the pictures are picked up from the HTML file's own directory,
so an edited pair renders exactly as it stands.

RenderHtmlDocument renders a complete HTML document held in memory, for a
caller who assembled the markup themselves - most often from
TexinfoHtmlResult.BodyHtml inside a page of their own. baseDirectory is what
relative stylesheet and picture references resolve against; null when the
document refers to no files.

For RenderHtmlFile and RenderHtmlDocument there was no Texinfo stage, so
TexinfoPdfResult.Intermediate is null and the warnings hold PDF-stage messages
only.

Nothing about a document's contents throws; both stages degrade and report.
Exceptions are the caller's own mistakes:

    ArgumentException         a null or blank file path or output path
    ArgumentNullException     a null texinfoSource, htmlResult or html
    FileNotFoundException     a Texinfo or HTML file that is not there

One renderer serves many documents. It is not safe across threads, and neither
are the two renderers it owns - give each thread its own TexinfoPdfRenderer.

TexinfoPdfOptions
-----------------

    TexinfoHtmlOptions  Texinfo   { get; }   //how the source is read
    HtmlRenderOptions   Html      { get; }   //what the PDF looks like

NEITHER IS A COPY. They are the live options objects of the two renderers
underneath, so every setting either library has - including any it gains later -
is reachable without a second package reference and without anything here having
to be kept in step. Whatever you set stays set for every later render through
the same TexinfoPdfRenderer.

Options.Texinfo is documented in full in the CodeBrix.Texinfo2Html AGENT-README
(EmitSingleFile, ConditionalProfile, IncludeSearchPaths, ImageSearchPaths,
PredefinedValues, NumberSections, ExtraCss, CssFileName, ImageFolderName,
SnippetRenderer). EmitSingleFile, CssFileName and ImageFolderName make no
difference to a PDF produced through this package - the document is embedded
and staged internally - but they still govern what WriteToDirectory writes if
you take the GenerateHtml path.

Options.Html is CodeBrix.PdfDocCreate.Html2Pdf's HtmlRenderOptions. Its
members, with the defaults as they stand when a TexinfoPdfRenderer is
constructed:

    double  PageWidthPoints         612     //US Letter; 72 points = 1 inch
    double  PageHeightPoints        792
    bool    Landscape               false   //swaps width and height at render
    double  MarginTopPoints         72
    double  MarginRightPoints       72
    double  MarginBottomPoints      72
    double  MarginLeftPoints        72
    string  HeaderText              "{title}"           <- set by this package
    string  FooterText              "{page} / {pages}"  <- set by this package
    bool    AllowRemoteImages       false   //http(s) <img> sources are not
                                            //fetched; Texinfo documents never
                                            //produce them anyway
    bool    GenerateOutline         true    //PDF bookmark pane from h1-h6
    SvgPlacementMode SvgPlacement   Vector  //Vector | Raster - see the
                                            //notice at the top of this file
    double  SvgRasterScale          2.0     //clamped to 0.25 - 8.0; the whole
                                            //picture's density in Raster mode,
                                            //and only a raster fallback's in
                                            //Vector mode
    bool    KeepUncoveredCharacters false   //see below
    string  DocumentTitle           null    //filled from @settitle when empty
    string  DocumentAuthor          null    //filled from first @author when empty

    void    SetPageSize(string name)        //"letter", "legal", "a4", "a5", ...;
                                            //throws ArgumentException for a
                                            //name it does not know

HeaderText and FooterText are centered on every page and expand the tokens
{page}, {pages} and {title}. Bare Html2Pdf renders neither by default; this
package sets both to the values above because a printed manual wants them. Set
either to an empty string (or null) to be rid of it.

DocumentTitle and DocumentAuthor are filled in from the document's own
@settitle and @author WHEN THE CALLER LEFT THEM EMPTY, and are put back to what
the caller had after every render - which is what stops one manual's title
following a reused renderer to the next. Set them yourself to override the
document.

KeepUncoveredCharacters: when false (default), a character no registered font
covers is removed from the PDF with a "font.uncovered.removed" warning; when
true it is kept and rendered as the font's missing-glyph shape (a visible tofu
box or blank) and the warning code is "font.uncovered.kept", so a coverage gap
leaves a trace in the document instead of silently changing the text.

An @page rule in the document's CSS (for example one you add through
Options.Texinfo.ExtraCss or a replacement stylesheet) overrides the page size
and margins configured on Options.Html. The stylesheet CodeBrix.Texinfo2Html
generates carries no @page rule, so by default Options.Html governs.

Anything Html2Pdf adds to HtmlRenderOptions later is reachable here without a
change to this package. The authoritative member list and the full semantics
are in that package's own AGENT-README:
https://github.com/ellisnet/CodeBrix.PdfDocuments/blob/main/src/CodeBrix.PdfDocCreate.Html2Pdf/AGENT-README.txt

TexinfoPdfResult
----------------

    string             OutputFilePath  //full path; "" for a render that produced bytes
    byte[]             PdfBytes        //null for a render that wrote a file
    int                PageCount
    string             Title           //the title the PDF carries in its metadata
    TexinfoHtmlResult  Intermediate    //null when the caller supplied the markup
    TexinfoPdfWarnings Warnings        //never null; empty for a clean document

Intermediate is the whole HTML/CSS result the PDF was made from, so a one-shot
conversion still gives access to the markup, the stylesheet and the picture
list without running the source twice.

TexinfoPdfWarnings
------------------

    IReadOnlyList<string>        Messages         //both stages, each tagged
    IReadOnlyList<string>        TexinfoMessages  //untagged, as Texinfo2Html wrote them
    IReadOnlyList<string>        PdfMessages      //untagged, as Html2Pdf wrote them
    IReadOnlyList<RenderWarning> PdfItems         //the PDF stage's structured warnings
    int                          Count            //total across both stages
    string                       ToString()       //all of Messages, one per line
    const string                 TexinfoStageTag = "[texinfo]"
    const string                 PdfStageTag     = "[pdf]"

Messages is the list to print: every Texinfo-stage message prefixed
"[texinfo] " and every PDF-stage message prefixed "[pdf] ", Texinfo stage
first because it ran first, source order kept within each stage.

TexinfoMessages have the shape "<Category>: <message> (at <file>:<line>:<col>)"
and open with one of the ten Texinfo2Html categories - Include, Conditional,
Macro, Value, RawBlockSkipped, Encoding, Syntax, UnknownCommand, Reference or
Emit - which is what to filter on; what each covers is set out in the
CodeBrix.Texinfo2Html AGENT-README. The Texinfo stage has no structured form.

PdfMessages have the shape "[<category>] <message>" with the lower-case
category word css, image, font or html. Duplicate messages are collapsed:
each distinct display message appears once, in first-occurrence order.

PdfItems is the PDF stage's warnings in structured form (type RenderWarning
from CodeBrix.PdfDocCreate.Html2Pdf):

    RenderWarningCategory Category     //Css | Image | Font | Html
    string                Code         //stable machine-readable code
    string                Message      //identical to the PdfMessages entry
    int                   Occurrences  //how many times this exact warning was raised
    int?                  CodePoint    //the Unicode code point, for glyph-coverage
                                       //warnings; otherwise null

    enum RenderWarningCategory { Css, Image, Font, Html }

Items are finer-grained than the prose: warnings sharing one display message
but concerning different code points are separate items. That is what lets a
test assert an exact drop baseline ("these N distinct code points, M
occurrences") instead of pattern-matching display prose, which is not a
compatibility surface. Codes ARE a compatibility surface. The codes this
package's own documentation and tests rely on:

    image.svg.rasterized         Vector mode: a part PDF cannot express as
                                 vectors was rasterized on its own; the rest
                                 of the picture stays vector
    image.svg.filter-unsupported an exotic filter primitive, or
                                 feTurbulence, was dropped by the SVG engine
    image.svg.text-unsupported   SVG text on a path, or a glyph-id text run;
                                 not drawn
    image.svg.fonts-missing      tripwire: the SVG engine had no font
                                 registered. Should not occur - report it
    image.svg.degraded           catch-all for any other SVG-engine degradation
    image.svg.empty              an <svg> without usable content; skipped
    image.svg.failed             the SVG could not be rendered; skipped
    font.uncovered.removed       a character no registered font covers was
                                 removed (KeepUncoveredCharacters = false)
    font.uncovered.kept          ... was kept as a missing-glyph shape
                                 (KeepUncoveredCharacters = true)
    font.svg-text.notdef         a character inside SVG text had no glyph
    image.format.unsupported     a picture in a format the PDF stage cannot
                                 decode

    RETIRED: "image.svg.nativemissing" can no longer be raised - there is no
    native library in the chain any more. Delete code that matches it.

The full code vocabulary belongs to CodeBrix.PdfDocCreate.Html2Pdf and is
enumerated in its AGENT-README:
https://github.com/ellisnet/CodeBrix.PdfDocuments/blob/main/src/CodeBrix.PdfDocCreate.Html2Pdf/AGENT-README.txt

Both surfaces pass the PDF stage's text through VERBATIM - nothing is trimmed,
re-wrapped or re-worded on the way out. A PDF-stage message names the exact
picture, filter, glyph or reason behind its code, and that detail is its whole
value; preserve that behaviour when you surface one.

A message means a different thing depending on which stage said it - the Texinfo
stage is talking about the source, the PDF stage about the markup or the fonts -
so filter on the split lists (or PdfItems) rather than pattern-matching the
merged one. On a manual quoting symbols no text font carries, [font] messages
are the expected ones.

TexinfoPdfFonts
---------------

Font registration for the PDF stage, forwarded to the Html2Pdf font registry so
a consumer of this package never has to name Html2Pdf. Registration is
process-global; all methods are idempotent and may be called before or after
renders have happened - additions take effect on the next render.

    static void AddFontDirectory(string directory)
    static void AddFontFile(string filePath, bool includeInFallback = false)
    static void AddFontFiles(IEnumerable<string> filePaths, bool includeInFallback = false)
    static void AddFontFilesFromDirectory(string directory, bool includeInFallback = false)
    static void AddFallbackFamily(string familyName)

AddFontDirectory probes a directory for CodeBrix.Platform.Fonts.* package
folders (the <Name>/Fonts/*.ttf + manifest layout those packages ship).

The AddFontFile family takes loose .ttf/.otf files with NO manifest - family
name, weight and style are read from the font's own tables - and either path
separator style works on every operating system. AddFontFiles groups faces
that share a family name into one family; AddFontFilesFromDirectory registers
every .ttf/.otf found directly in the directory. includeInFallback = true also
appends the family to the per-glyph fallback chain.

AddFallbackFamily appends an ALREADY-REGISTERED family to the per-glyph
fallback chain: when a character has no glyph in the font a run resolved to,
the fallback families are consulted in registration order and the first one
covering it renders that character. Fallback families never substitute whole
runs - only individual characters.

A registered font is usable from the generated markup's font families (name it
in Options.Texinfo.ExtraCss or a replacement stylesheet), from SVG text inside
placed pictures, and - when opted in - from the fallback chain. The PDF stage
already brings the Roboto, Merriweather, RobotoMono and NotoMusic packages
along; Noto Music sits in the fallback chain automatically, which is what
renders a manual's flat, natural and sharp signs and the supplementary-plane
music symbols. The PDF stage never falls back to operating-system fonts: a
script no registered font covers is dropped (or kept as tofu) with a
font.uncovered.* warning, never substituted from the system.


COMPLETE EXAMPLES
=================

(a) One shot - source in, PDF out

    using System;
    using CodeBrix.Texinfo2Pdf;

    internal static class Program
    {
        private static void Main(string[] args)
        {
            var renderer = new TexinfoPdfRenderer();
            TexinfoPdfResult result = renderer.RenderFile("manual.texi", "out/manual.pdf");

            Console.WriteLine($"{result.PageCount} pages, {result.Warnings.Count} warnings");
            foreach (string warning in result.Warnings.Messages)
            {
                Console.WriteLine(warning);   //"[texinfo] ..." and "[pdf] ..."
            }
            //out/ holds one PDF and nothing else. Leave the output path off and
            //the PDF is written beside the source as manual.pdf.
        }
    }

(b) Page setup, running header/footer, predefined values, in-memory output

    using System.IO;
    using CodeBrix.Texinfo2Pdf;

    var renderer = new TexinfoPdfRenderer();
    renderer.Options.Html.SetPageSize("a4");
    renderer.Options.Html.MarginLeftPoints = 54;    //0.75 inch
    renderer.Options.Html.MarginRightPoints = 54;
    renderer.Options.Html.HeaderText = "";          //no running title
    renderer.Options.Html.FooterText = "Page {page} of {pages}";
    renderer.Options.Html.DocumentAuthor = "Documentation Team";   //overrides @author
    renderer.Options.Texinfo.PredefinedValues["VERSION"] = "2.1";  //as @set VERSION 2.1
    renderer.Options.Texinfo.IncludeSearchPaths.Add("/abs/path/to/shared-includes");

    TexinfoPdfResult pdf = renderer.RenderFileToBytes("manual.texi");
    File.WriteAllBytes("manual.pdf", pdf.PdfBytes);   //or send it somewhere

(c) Restyle the intermediate before it becomes a PDF

    using CodeBrix.Texinfo2Html;
    using CodeBrix.Texinfo2Pdf;

    var renderer = new TexinfoPdfRenderer();
    TexinfoHtmlResult html = renderer.GenerateHtmlFromFile("manual.texi");

    //html.BodyHtml, html.Css, html.Title, html.Images and html.Warnings are all here
    string myCss = html.Css.Replace("#111111", "#000033");   //or replace it wholesale

    TexinfoPdfResult pdf = renderer.RenderHtml(html, "out/manual.pdf", myCss);

The pictures need no handling at all: RenderHtml stages them in a temporary
directory for the length of the render and sweeps it up afterwards, so what
lands in "out" is one PDF and nothing else. That is the whole reason this
library exists rather than a paragraph of instructions.

(d) Edit the files on disk, then come back in

    using CodeBrix.Texinfo2Html;
    using CodeBrix.Texinfo2Pdf;

    var renderer = new TexinfoPdfRenderer();
    TexinfoHtmlResult html = renderer.GenerateHtmlFromFile("manual.texi");
    string path = html.WriteToDirectory("work");     //work/manual.html + work/manual.css
                                                     //+ work/manual-images/...
    //...edit work/manual.html and work/manual.css by hand...
    renderer.RenderHtmlFile(path, "out/manual.pdf"); //stylesheet and pictures picked
                                                     //up from work/

(e) Engrave the music of a .tely manual

    using CodeBrix.Texinfo2Html;
    using CodeBrix.Texinfo2Pdf;

    public sealed class MyEngraver : ILilypondSnippetRenderer
    {
        public LilypondSnippetResult Render(LilypondSnippet snippet)
        {
            byte[] png = MyLilypondDriver.EngraveToPng(snippet);   //your code
            return png == null
                ? LilypondSnippetResult.Failed("LilyPond produced no output.")
                : LilypondSnippetResult.FromContent(png, "png");
        }
    }

    var renderer = new TexinfoPdfRenderer();
    renderer.Options.Texinfo.SnippetRenderer = new MyEngraver();
    renderer.RenderFile("notation.tely", "out/notation.pdf");   //pictures travel along

The implementer's full contract (LilypondSnippet, LilypondSnippetOptions,
LilypondSnippetResult, LilypondSnippetImage) is in the CodeBrix.Texinfo2Html
AGENT-README.

(f) Register fonts, then handle warnings by stage and by code

    using System;
    using System.Linq;
    using CodeBrix.PdfDocCreate.Html2Pdf;
    using CodeBrix.Texinfo2Pdf;

    TexinfoPdfFonts.AddFontFile("/fonts/MyCorporateSerif-Regular.ttf");
    TexinfoPdfFonts.AddFontFile("/fonts/MyCorporateSerif-Bold.ttf");
    TexinfoPdfFonts.AddFontFilesFromDirectory("/fonts/noto-extras", includeInFallback: true);

    var renderer = new TexinfoPdfRenderer();
    renderer.Options.Texinfo.ExtraCss =
        "html, h1, h2, h3, h4, h5, h6 { font-family: 'MyCorporateSerif', serif; }";

    TexinfoPdfResult result = renderer.RenderFile("manual.texi", "out/manual.pdf");

    //source problems, by Texinfo category
    var syntax = result.Warnings.TexinfoMessages
        .Where(m => m.StartsWith("Syntax:", StringComparison.Ordinal)).ToList();

    //typesetting problems, structured
    RenderWarning svgRasterized = result.Warnings.PdfItems
        .FirstOrDefault(i => i.Code == "image.svg.rasterized");
    if (svgRasterized != null)
    {
        Console.Error.WriteLine(svgRasterized.Message);   //verbatim; names the reason
        Console.Error.WriteLine($"{svgRasterized.Occurrences} raster fallback(s)");
    }
    int droppedCodePoints = result.Warnings.PdfItems
        .Count(i => i.Category == RenderWarningCategory.Font && i.CodePoint.HasValue);


MINIMUM VIABLE PROJECT
======================

texi2pdf.csproj

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>disable</Nullable>
        <ImplicitUsings>disable</ImplicitUsings>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.Texinfo2Pdf.MitLicenseForever" Version="*" />
      </ItemGroup>
    </Project>

Program.cs

    using System;
    using CodeBrix.Texinfo2Pdf;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: texi2pdf <source.texi> [output.pdf]");
                return 2;
            }
            var renderer = new TexinfoPdfRenderer();
            TexinfoPdfResult result = renderer.RenderFile(args[0],
                args.Length > 1 ? args[1] : null);
            foreach (string warning in result.Warnings.Messages)
            {
                Console.Error.WriteLine(warning);
            }
            Console.WriteLine($"{result.OutputFilePath}: {result.PageCount} pages");
            return 0;
        }
    }

    dotnet run -- docs/manual.texi out/manual.pdf

(Replace Version="*" with the current versions from nuget.org when you pin.)


PERFORMANCE TIPS
================

  * Scale, so nobody has to guess: the LilyPond notation reference - 51,000
    lines in, 965 pages out - takes on the order of SIX SECONDS end to end on
    a developer laptop. Reading and parsing the Texinfo is a fraction of a
    second of that; the time is the typesetting, and it is Html2Pdf's.
  * Cost is linear in document size. The test suite renders synthetic manuals
    of 500 and 2,000 sections purely to fail if anything ever goes quadratic.
  * A reused renderer accumulates nothing between documents; reading the same
    manual twice through one renderer costs the same both times. Construct one
    TexinfoPdfRenderer per thread and reuse it; never share one across
    threads.
  * GenerateHtml + RenderHtml costs the same as RenderFile; the Texinfo stage
    runs once either way. Intermediate on a one-shot result is free - it is
    the object the PDF was made from, not a second render.
  * Font registration is process-global and idempotent; do it once at startup,
    not per render.
  * A manual of any size is comfortable in a build step or an offline job.
    Rendering a 965-page manual inside a web request is not what any of this
    is for.


COMMON PITFALLS TO AVOID
========================

  * ASSUMING SVG IS RASTERIZED. By default it is not: SVG is placed as PDF
    vector content and adds no image XObject to the file. Set
    Options.Html.SvgPlacement = SvgPlacementMode.Raster when a bitmap is what
    you want. Nothing about SVG needs a native package, on any OS.
  * OPTIONS ARE LIVE, NOT COPIES. Options.Texinfo and Options.Html belong to
    the renderers underneath and keep whatever you set for every later render.
    (DocumentTitle / DocumentAuthor are the one exception: values filled in
    from the document are put back afterwards.)
  * ONE RENDERER PER THREAD. TexinfoPdfRenderer and both renderers it owns are
    not thread-safe.
  * EXPECTING EXCEPTIONS FOR BAD DOCUMENTS. Nothing about a document's
    contents throws, in either stage. A build gate has to read
    result.Warnings.
  * PATTERN-MATCHING THE MERGED Messages LIST. A message means something
    different depending on the stage; filter TexinfoMessages by category
    prefix and PdfItems by Code. Display prose is not a compatibility surface;
    codes and category words are.
  * RE-WRAPPING A PDF-STAGE MESSAGE. Pass the text of a Warnings.PdfItems
    entry through verbatim; the picture, reason or code point it names is its
    whole value.
  * A WRONG BASE DIRECTORY WITH RenderHtmlDocument. The generated markup's
    image paths are relative to the document; the base directory you pass must
    be the directory the pictures were copied into (CopyImagesTo), not the
    directory the source was read from. RenderFile / RenderHtml /
    RenderHtmlFile never have this problem.
  * EXPECTING A PICTURES FOLDER BESIDE THE PDF. There is none - staging is
    temporary and swept up. If you want the HTML, CSS and pictures on disk,
    use GenerateHtmlFromFile + WriteToDirectory.
  * EXPECTING TEXT IN AN UNCOVERED SCRIPT TO APPEAR. The PDF stage never falls
    back to system fonts. Register a font that covers the script
    (TexinfoPdfFonts.AddFontFile(..., includeInFallback: true)) or set
    Options.Html.KeepUncoveredCharacters = true to at least leave a visible
    trace.
  * EXPECTING @page IN YOUR CSS TO BE IGNORED. It is not: an @page rule in
    ExtraCss or a replacement stylesheet overrides Options.Html's page size
    and margins.
  * FORGETTING THAT Intermediate IS NULL for RenderHtmlFile and
    RenderHtmlDocument - there was no Texinfo stage.
  * EXPECTING MUSIC TO BE ENGRAVED. With no Options.Texinfo.SnippetRenderer a
    .tely snippet is its source text, by design.
  * ADDING A SECOND PackageReference TO CodeBrix.Texinfo2Html. Not needed; it
    flows through this package. Harmless if versions agree, a restore conflict
    if they do not.


WHAT THIS PACKAGE DOES NOT DO
=============================

  * It does not parse, emit or restyle anything itself. Everything about how
    Texinfo becomes markup - and therefore every limitation listed under WHAT
    THIS PACKAGE DOES NOT DO in the CodeBrix.Texinfo2Html AGENT-README (no Info
    output, no website split, @math as styled text, @documentencoding reported
    not obeyed, raw blocks skipped, no music engraving without a registered
    renderer) - applies here unchanged.
  * It does not need a native library of any kind, and neither does anything
    under it; there is nothing for a consuming application to add.
  * It does not fall back to operating-system fonts, and neither does the PDF
    stage under it. Only registered fonts render.
  * It does not fetch remote images (AllowRemoteImages is off, and Texinfo
    never writes such references).
  * It does not expose the PDF stage's renderer directly. Everything you can
    set on it is reachable through Options.Html; everything it reported is in
    Warnings.PdfItems.
  * It does not write the HTML, CSS or pictures anywhere you can see them
    during a PDF render. Use the GenerateHtml path when you want them.
  * It does not run LilyPond, and never will.


WORKING EXAMPLES ON GITHUB
==========================

    https://github.com/ellisnet/CodeBrix.Texinfo/tree/main/tests/CodeBrix.Texinfo2Pdf.Tests

    TexinfoPdfRendererTests.cs      the composition API from source written for
                                    it: RenderFile beside the source and into a
                                    created directory, RenderTexinfo,
                                    RenderFileToBytes, RenderTexinfoToBytes,
                                    Intermediate on a one-shot result,
                                    GenerateHtml / GenerateHtmlFromFile,
                                    RenderHtml with and without a replacement
                                    stylesheet, RenderHtmlFile, RenderHtmlDocument,
                                    options reaching both stages, the title/
                                    author fill-in and its undo, the stage-
                                    tagged warning merge, the caller mistakes
                                    that throw
    PdfFeaturePassThroughTests.cs   an SVG and a WebP picture travelling into
                                    the PDF, the vector-SVG canary (no native
                                    library needed, no image XObject and no
                                    warning), SvgRasterScale and
                                    KeepUncoveredCharacters through the live
                                    Options.Html, structured PdfItems (Code,
                                    CodePoint, Occurrences), TexinfoPdfFonts
                                    forwarding
    SnippetToPdfGateTests.cs        a registered ILilypondSnippetRenderer whose
                                    PNG and SVG pictures land in a real PDF (the
                                    SVG as vector content, no image XObject); a
                                    document with no engraver rendering its
                                    snippets as source. The PNG is built by the
                                    test (TestPng.cs) from first principles.
    TexinfoToPdfGateTests.cs        the end-to-end gate on real manuals, the
                                    glyph-coverage check, a document that uses
                                    every general-Texinfo feature at once, a
                                    written-out manual taking its pictures with
                                    it, a self-contained document rendering
                                    without a stylesheet file
    NotationStressTests.cs          the scale tests: the notation reference
                                    within a time budget, read twice through one
                                    renderer, and the synthetic linear-cost check

The corpus-based tests need the English LilyPond documentation, which is not
in the repository, and skip when it is absent; the rest are self-contained.


QUICK REFERENCE CARD
====================

    dotnet add package CodeBrix.Texinfo2Pdf.MitLicenseForever
    # nothing else on any OS - the whole chain is managed code
    using CodeBrix.Texinfo2Pdf;      //+ CodeBrix.Texinfo2Html for TexinfoHtmlResult etc.

    var r = new TexinfoPdfRenderer();                //one per thread; reusable

    // options (LIVE objects - settings persist across renders)
    r.Options.Html.SetPageSize("a4");                //"letter" "legal" "a4" "a5" ...
    r.Options.Html.Landscape = true;
    r.Options.Html.PageWidthPoints / PageHeightPoints        (612 x 792)
    r.Options.Html.MarginTopPoints / Right / Bottom / Left   (72 each)
    r.Options.Html.HeaderText = "{title}";           //default here; "" to remove
    r.Options.Html.FooterText = "{page} / {pages}";  //default here; "" to remove
    r.Options.Html.DocumentTitle / DocumentAuthor    //else from @settitle / @author
    r.Options.Html.GenerateOutline = true;           //bookmarks from headings
    r.Options.Html.SvgPlacement = SvgPlacementMode.Vector;  //default; Raster = bitmap
    r.Options.Html.SvgRasterScale = 2.0;             //0.25 - 8.0; Raster mode, and any
                                                     //raster fallback in Vector mode
    r.Options.Html.KeepUncoveredCharacters = false;  //true: tofu instead of drop
    r.Options.Html.AllowRemoteImages = false;
    r.Options.Texinfo.PredefinedValues["V"] = "1";   //= @set V 1
    r.Options.Texinfo.IncludeSearchPaths / ImageSearchPaths / ExtraCss
    r.Options.Texinfo.ConditionalProfile / NumberSections
    r.Options.Texinfo.SnippetRenderer = new MyEngraver();

    // workflow one
    TexinfoPdfResult p = r.RenderFile("m.texi", "out/m.pdf");   //null path: beside source
    TexinfoPdfResult p = r.RenderTexinfo(src, "out/m.pdf", baseDir);
    TexinfoPdfResult p = r.RenderFileToBytes("m.texi");         //p.PdfBytes
    TexinfoPdfResult p = r.RenderTexinfoToBytes(src, baseDir);
    // workflow two
    TexinfoHtmlResult h = r.GenerateHtmlFromFile("m.texi");     //or r.GenerateHtml(src, baseDir)
    TexinfoPdfResult p = r.RenderHtml(h, "out/m.pdf", myCss);   //myCss null = generated
    TexinfoPdfResult p = r.RenderHtmlToBytes(h, myCss);
    TexinfoPdfResult p = r.RenderHtmlFile("work/m.html", "out/m.pdf");
    TexinfoPdfResult p = r.RenderHtmlDocument(htmlString, "out/m.pdf", baseDir);

    p.OutputFilePath  p.PdfBytes  p.PageCount  p.Title  p.Intermediate  p.Warnings
    p.Warnings.Messages          //"[texinfo] ..." then "[pdf] ..."
    p.Warnings.TexinfoMessages   //"Category: message (at file:line:col)"
    p.Warnings.PdfMessages       //"[css|image|font|html] message"
    p.Warnings.PdfItems          //RenderWarning: Category Code Message Occurrences CodePoint
    p.Warnings.Count  p.Warnings.ToString()
    TexinfoPdfWarnings.TexinfoStageTag == "[texinfo]"   TexinfoPdfWarnings.PdfStageTag == "[pdf]"

    // fonts (process-global, idempotent)
    TexinfoPdfFonts.AddFontDirectory(dir)                     //package-shaped folders
    TexinfoPdfFonts.AddFontFile(path, includeInFallback: false)
    TexinfoPdfFonts.AddFontFiles(paths, includeInFallback: false)
    TexinfoPdfFonts.AddFontFilesFromDirectory(dir, includeInFallback: false)
    TexinfoPdfFonts.AddFallbackFamily("Family Name")          //already registered

    Throws only for caller mistakes: ArgumentException (blank path),
    ArgumentNullException (null source/result/html), FileNotFoundException.
    Codes to know: image.svg.rasterized  image.svg.filter-unsupported
                   image.svg.text-unsupported  image.svg.fonts-missing
                   font.uncovered.removed  font.uncovered.kept
                   font.svg-text.notdef  image.format.unsupported

================================================================================
