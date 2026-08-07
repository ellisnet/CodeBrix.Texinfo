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

This repository is under construction, and the two libraries are at very
different stages.

CodeBrix.Texinfo2Html HAS A PUBLIC API and renders Texinfo to HTML and CSS
end to end. Its whole pipeline is in place:

  Sources/        source loading, encoding and line-ending handling
  Lexing/         a lossless Texinfo lexer with raw-block capture
  Preprocessing/  @include with search paths, @set/@clear/@value, conditional
                  profiles, raw output blocks, comments, @verbatiminclude, and
                  full @macro/@rmacro/@unmacro/@alias expansion
  Parsing/        the parser and its table of built-in commands
  Model/          the parsed document tree - sections, blocks, inline runs,
                  plus the anchor, index, footnote and settings tables
  Semantics/      section numbering, HTML identifier allocation, heading
                  ranking and table-of-contents construction
  Emit/           the HTML emitter, the default print stylesheet, the document
                  builder and image reference resolution
  Diagnostics/    collected warnings, used instead of exceptions throughout

Everything except the types named in CORE API REFERENCE below is internal, and
is exercised directly by the test project through InternalsVisibleTo.

CodeBrix.Texinfo2Pdf STILL CONTAINS ONLY InternalsVisibleTo.cs. It has no
public API. A consumer who wants a PDF today renders to HTML and CSS with
CodeBrix.Texinfo2Html and hands the result to
CodeBrix.PdfDocCreate.Html2Pdf.HtmlPdfRenderer, which is exactly what the
end-to-end gate test in tests/CodeBrix.Texinfo2Pdf.Tests does.

What CodeBrix.Texinfo2Html does NOT do yet, so that no agent documents it as
though it did:

  * Cross references (@ref, @xref, @pxref) render as their visible text and do
    not link to their destination. The destinations themselves exist: every
    node, anchor and section already carries a unique HTML identifier.
  * Indices are collected but not rendered. @printindex produces nothing and
    one warning.
  * @lilypond and @lilypondfile snippets are emitted as their source text in a
    preformatted block, with one warning per document. There is no renderer
    seam for engraving them yet.
  * Texinfo's text conventions for dashes and quotation marks (---, --, `` and
    '') are passed through as written rather than converted.
  * Accent commands (@'e, @"o and the rest) are not implemented. The 47
    no-argument glyph commands are.
  * The definition-command family (@deffn, @defun, ...) and @float parse as
    plain block environments and warn once each.

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

Every public type of CodeBrix.Texinfo2Html sits in that one root namespace; the
sub-namespaces underneath it (Sources, Lexing, Preprocessing, Parsing, Model,
Semantics, Emit, Diagnostics) are all internal. CodeBrix.Texinfo2Pdf has no
public types yet. There is no separate CodeBrix.Texinfo namespace and no
project named CodeBrix.Texinfo - that name belongs to the repository and to the
solution file only.


CORE API REFERENCE - CodeBrix.Texinfo2Html
--------------------------------------------------------------------------------

Five public types, all in the CodeBrix.Texinfo2Html namespace.

--- TexinfoHtmlRenderer ---

    var renderer = new TexinfoHtmlRenderer();

    TexinfoHtmlResult GenerateFromFile(string texinfoFilePath)
    TexinfoHtmlResult Generate(string texinfoSource, string baseDirectory = null)
    TexinfoHtmlOptions Options { get; }

One renderer can be reused for many documents; set Options before calling.
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

PredefinedValues acts as though the source opened with @set name value, which
is how to supply the version and date strings a manual's build normally
generates into an included file. ExtraCss is appended after the built-in
stylesheet, so a repeated rule of equal specificity wins.

--- TexinfoHtmlResult ---

    string Html            //the complete document
    string BodyHtml        //the generated markup on its own
    string Css             //always separate, even when it was embedded
    string Title           //from @settitle
    string BaseDirectory
    string CssFileName
    TexinfoRenderWarnings Warnings

    string ToHtmlDocument(string replacementCss)
    string WriteToDirectory(string directory, string baseName = null)

WriteToDirectory creates the directory if needed, writes <baseName>.html, and
writes the stylesheet beside it under CssFileName unless EmitSingleFile was
set. It returns the full path of the HTML file.

--- TexinfoRenderWarnings ---

    IReadOnlyList<string> Messages
    int Count

--- TexinfoConditionalProfile ---

    Print   //@iftex and every @ifnot... branch; the right one for PDF output
    Html    //@ifhtml on, @ifnothtml off

Print deliberately reads BOTH @iftex and @ifnottex, because real manuals put
document structure - most often the @node Top and @top pair the whole document
hangs from - in the @ifnottex branch. The cost is that a document writing the
same visible content into both branches contributes it twice.

--- The two workflows ---

    //(a) straight to a PDF
    var result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
    var htmlPath = result.WriteToDirectory("out");        //manual.html + manual.css
    new HtmlPdfRenderer().RenderFile(htmlPath, "out/manual.pdf");

    //(b) restyle the intermediate first
    var result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
    var myCss = result.Css.Replace("#111111", "#000033"); //or replace wholesale
    var html = result.ToHtmlDocument(myCss);
    new HtmlPdfRenderer().RenderHtml(html, "out/manual.pdf", result.BaseDirectory);

Image references are written into the markup as absolute paths, resolved at
generation time, so the HTML renders from wherever it is put. That does mean
the .html/.css pair is not portable to another machine on its own; passing
result.BaseDirectory to Html2Pdf costs nothing and is the right habit for when
that changes.


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

The intended split between the two libraries:

  CodeBrix.Texinfo2Html owns everything about the Texinfo format - lexing the
  @-commands, resolving cross references and node structure, handling the .tely
  dialect's LilyPond blocks, and emitting the HTML and CSS. It knows nothing
  about PDF.

  CodeBrix.Texinfo2Pdf owns the hand-off to CodeBrix.PdfDocCreate.Html2Pdf and
  the options that govern the finished document. It knows nothing about the
  Texinfo format beyond passing the source through to CodeBrix.Texinfo2Html.

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
      every manual renders to markup with only the expected warnings, and
      every table-of-contents link has a destination in the same document.

  TexinfoToPdfGateTests            (CodeBrix.Texinfo2Pdf.Tests)
      the end-to-end gate: Texinfo -> HTML/CSS -> PDF, with Html2Pdf reporting
      nothing but font-coverage messages. This is the test that proves the two
      libraries agree on the markup subset; nothing inside
      CodeBrix.Texinfo2Html alone can show that. It leaves the PDFs it built in
      <temp>/codebrix-texinfo-gate so they can be looked at afterwards.

Fixtures committed to this repository must be original work written for the
test, or come from an explicitly MIT, CC0 or public-domain source listed in
THIRD-PARTY-NOTICES.txt. Nothing GFDL, nothing GPL.

Run the whole suite from the repository root:

    dotnet test CodeBrix.Texinfo.slnx


================================================================================
