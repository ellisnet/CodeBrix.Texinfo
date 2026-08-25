================================================================================
MAINTAINER-README: CodeBrix.Texinfo
Notes for people and agents MAINTAINING this repository — not for package consumers
================================================================================


PURPOSE AND SCOPE
=================

This repository produces two NuGet packages, one per library project:

    CodeBrix.Texinfo2Html.MitLicenseForever
        src/CodeBrix.Texinfo2Html/        Texinfo source -> HTML and CSS
        Consumer documentation:           AGENT-README.txt (repository root)

    CodeBrix.Texinfo2Pdf.MitLicenseForever
        src/CodeBrix.Texinfo2Pdf/         Texinfo source -> PDF, in one step
        Consumer documentation:           src/CodeBrix.Texinfo2Pdf/AGENT-README.txt

Both are original implementations. There is no project, assembly or namespace
named CodeBrix.Texinfo - that name belongs to the repository and the solution
file only.

The split between the two libraries is a rule, not just a description:

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

Update the relevant AGENT-README as the public API grows. Document what is
true, never what is planned.


REPOSITORY LAYOUT
=================

    CodeBrix.Texinfo.slnx                     Solution - four projects
    global.json                               Selects the test runner; see TESTING
    AGENT-README.txt                          Consumer guide: Texinfo2Html package
    MAINTAINER-README.txt                     This file
    EXTRAS-README.txt                         Non-package content (there is none)
    README-INDEX.txt                          Map of the README files
    README.md                                 Human-facing overview (GitHub, nuget.org)
    LICENSE, THIRD-PARTY-NOTICES.txt, icon-codebrix-128.png

    src/CodeBrix.Texinfo2Html/                Renders Texinfo to HTML and CSS
        Sources/        source loading, encoding and line-ending handling
        Lexing/         a lossless Texinfo lexer with raw-block capture
        Preprocessing/  @include with search paths, @set/@clear/@value,
                        conditional profiles, raw output blocks, comments,
                        @verbatiminclude, and full @macro/@rmacro/@linemacro/
                        @unmacro/@alias expansion
        Parsing/        the parser and its table of built-in commands
        Model/          the parsed document tree - sections, blocks, inline
                        runs, plus the anchor, index, footnote and settings
                        tables
        Semantics/      section numbering, HTML identifier allocation, heading
                        ranking, table-of-contents construction, footnote
                        placement, float numbering, and index building
                        (merges, sort keys, ordering)
        Snippets/       the lilypond-book option list, and the coordinator that
                        hands music environments to a registered engraver
        Emit/           the HTML emitter, the default print stylesheet, the
                        document builder, image reference resolution and the
                        text conventions
        Diagnostics/    collected warnings, used instead of exceptions
                        throughout
        (root)          the twelve public types, InternalsVisibleTo.cs
    src/CodeBrix.Texinfo2Pdf/                 Renders Texinfo to PDF
        Rendering/      ImageStagingArea, the internal temporary-directory
                        helper
        (root)          the five public types, InternalsVisibleTo.cs,
                        AGENT-README.txt

    tests/CodeBrix.Texinfo2Html.Tests/        xUnit v3 tests for Texinfo2Html
    tests/CodeBrix.Texinfo2Pdf.Tests/         xUnit v3 tests for Texinfo2Pdf

    TestResults/                              Local output folder left by
                                              "dotnet test"; not source

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
============

Inside CodeBrix.Texinfo2Html the stages run in one order and each owns one
sub-folder and matching sub-namespace: Sources -> Lexing -> Preprocessing ->
Parsing (into Model) -> Semantics -> Emit, with Diagnostics collecting warnings
throughout and Snippets consulted by Emit. Keep that shape. The parser records
only what the source said; the semantic pass works out what it meant across the
whole document (numbering, identifiers, the contents); the emitter writes markup
and decides nothing about meaning. Only the twelve public types sit at the
project root; everything else is internal and is exercised directly by the test
project through InternalsVisibleTo. Keep new source files organized into
sub-folders and matching sub-namespaces; only the public entry-point types
belong at a project root.

The HTML and CSS that CodeBrix.Texinfo2Html emits is a contract with
CodeBrix.PdfDocCreate.Html2Pdf, not general-purpose web markup. Html2Pdf applies
a documented subset of CSS - inline style attributes, style blocks and linked
local stylesheets, with real selector matching, cascade, specificity and
inheritance - and it renders all text through the CodeBrix.Platform.Fonts
packages so output is identical on every operating system. Stay inside that
subset. When you are unsure whether a construct is supported, check the
Html2Pdf AGENT-README rather than guessing; markup that a browser tolerates is
not automatically markup that Html2Pdf renders. The test that proves the two
libraries agree on the subset is TexinfoToPdfGateTests (see TESTING).

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
  * TexinfoPdfOptions.Texinfo and .Html are the LIVE option objects of the two
    renderers underneath, not copies. Do not "improve" this into a copy: the
    drift is the bug it was written to avoid. The one thing the render
    modifies on them - DocumentTitle / DocumentAuthor filled in from the
    document - is restored in a finally block after every render.

Design decisions in the emitter that look like bugs and must NOT be "fixed":

  * A DEFINITION'S CATEGORY IS WRITTEN AT THE HEAD OF THE LINE, not floated to
    the right margin as Texinfo prints it. That needs a floating box the output
    subset has not got. Do not turn a definition into a table: a definition
    sits inside lists, quotations and other definitions, and a table there is a
    layout trap.
  * A NESTED PREFORMATTED BLOCK IS ONE MORE STEP OF INDENTATION (five spaces),
    not a second <pre>. No browser accepts a nested <pre> and Html2Pdf is not
    being asked to. The inner block stays a node of its own rather than being
    flattened into text because it keeps its OWN text conventions.
  * THE quote SNIPPET OPTION IS AN INLINE STYLE, NOT A CONTAINER. A bordered
    container is laid out by Html2Pdf as one box, and that is what silently
    swallowed multitables inside @quotation until it was written this way. Do
    not "improve" this into a <div>.
  * @linemacro ARGUMENTS SPLIT ON SPACES, not on the commas every other
    invocation form uses. It is a different rule, not an oversight.
  * .pdf IS NOT IN THE @image EXTENSION PROBE. A manual that keeps pdf/NAME
    variants for its TeX branch would otherwise hand Html2Pdf a file it cannot
    decode.
  * TexinfoConditionalProfile.Print READS BOTH @iftex AND @ifnottex. Real
    manuals put the document structure in @ifnottex and their TeX machinery in
    raw @tex blocks; reading both branches yields the complete set of
    definitions. The cost (duplicated visible content when a manual writes the
    same thing into both) is accepted.
  * SNIPPET TROUBLE IS COUNTED, NOT LISTED. SnippetRenderCoordinator produces
    one warning per kind of trouble for the whole document; a misconfigured
    engraver would otherwise bury every other warning under thousands.
  * WARNINGS, NEVER EXCEPTIONS, for anything in a document. Exceptions are for
    caller mistakes only (blank path, missing file, null argument).

The Linux SkiaSharp native-assets requirement is DELIBERATELY undeclared. Two
mutually exclusive variants exist (SkiaSharp.NativeAssets.Linux and
SkiaSharp.NativeAssets.Linux.NoDependencies), only the consuming application
can choose between them, and CodeBrix.PdfDocCreate.Html2Pdf underneath does
not declare one either. Do NOT "fix" this by adding a PackageReference to
src/CodeBrix.Texinfo2Pdf; the commented-out reference and the explanatory
comment in that csproj are there to stop exactly that. The consumer-facing
explanation lives in src/CodeBrix.Texinfo2Pdf/AGENT-README.txt and README.md.


BUILDING
========

Standard SDK build, from the repository root:

    dotnet restore CodeBrix.Texinfo.slnx
    dotnet build CodeBrix.Texinfo.slnx

Both libraries target net10.0 only. GeneratePackageOnBuild is true on both
library projects, so every build of a library project also produces a .nupkg
(see PACKAGING AND PUBLISHING). There are no build scripts, no
Directory.Build.props and no extra targets.


TESTING
=======

Run the whole suite from the repository root:

    dotnet test --solution CodeBrix.Texinfo.slnx

The test projects run on Microsoft.Testing.Platform (xunit.v3), which no longer
supports the legacy VSTest bridge on the .NET 10 SDK. That makes the
global.json beside the .slnx load-bearing: it selects that runner for
"dotnet test", has no "sdk" section, and pins no SDK version - runner selection
is the only thing it is there for. Do NOT delete it. Without it "dotnet test"
fails outright with "Testing with VSTest target is no longer supported by
Microsoft.Testing.Platform on .NET 10 SDK and later".

Naming the target explicitly - "--solution <file>.slnx", or "--project
<file>.csproj" for one project - is the form to prefer and the one to write in
scripts.

Each test project also builds to an executable, so a single suite can be run
directly without the SDK test host at all:

    tests/CodeBrix.Texinfo2Pdf.Tests/bin/Debug/net10.0/CodeBrix.Texinfo2Pdf.Tests

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
  * Tests use SilverAssertions; prefer the fluent form -
    'actual.Should().Be(expected)' over 'Assert.Equal(expected, actual)'.

NATIVE ASSETS IN THE TEST PROJECTS: tests/CodeBrix.Texinfo2Pdf.Tests carries a
PackageReference to SkiaSharp.NativeAssets.Linux.NoDependencies so the suite's
SVG tests pass on Linux. That is NOT a recommendation of that variant - the
other one would serve equally well; it was chosen only because it is
self-contained on the build machine - and that reference belongs ONLY in
tests/, never in src/. tests/CodeBrix.Texinfo2Html.Tests deliberately has no
such reference, because nothing in that library reaches SkiaSharp.

THE CORPUS (optional, external, never committed): several suites need the
English LilyPond documentation, which is GFDL-licensed and so is never
committed here. They read it from ~/GitHome/lilypond/Documentation and skip
cleanly when it is absent. The corpus is eight manuals, about 110,000 lines
across 123 files, the largest the 51,000-line notation reference; its two macro
files define 158 macros and it holds 389 snippet files. The general-Texinfo
corpus documents (the GNU Texinfo manual, the GNU Make manual) are likewise
read locally and never committed.

  LilypondCorpusGateTests           (Texinfo2Html.Tests)
      the corpus macro files define the expected macro table and expand
      representative invocations correctly.

  LilypondParserCorpusGateTests     (Texinfo2Html.Tests)
      every manual parses with only the expected warnings; the whole corpus
      parses without a single structural warning.

  LilypondEmitterCorpusGateTests    (Texinfo2Html.Tests)
      every manual renders to markup with only the expected warnings; EVERY
      internal link in the output - contents, cross reference or index line -
      has a destination in the same document; and the manuals that print an
      index print one of the expected size.

  TexinfoToPdfGateTests             (Texinfo2Pdf.Tests)
      the end-to-end gate: Texinfo -> HTML/CSS -> PDF, with Html2Pdf reporting
      nothing but font-coverage messages. This is the test that proves the two
      libraries agree on the markup subset; nothing inside
      CodeBrix.Texinfo2Html alone can show that. It runs THROUGH THE SHIPPED
      CodeBrix.Texinfo2Pdf API rather than through a chain the test assembles,
      so it gates what a consumer actually gets - keep it that way. It also
      holds the glyph coverage check: no character in a script the
      CodeBrix.Platform.Fonts packages cover may be dropped from a PDF. The
      musical accidental signs render through the Noto Music fallback family,
      so the only thing the corpus drops is a Hebrew lyric quoted inside a
      snippet - no registered font carries that script, and CodeBrix never
      falls back to a system font. It leaves the PDFs it built in
      <temp>/codebrix-texinfo-gate so they can be looked at afterwards. The
      GNU Texinfo manual renders with four warnings (three skipped @tex blocks
      and one @math); the GNU Make manual with none.

  NotationStressTests               (Texinfo2Pdf.Tests)
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

FIXTURE-BASED SUITES (no corpus needed):

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

SnippetToPdfGateTests (Texinfo2Pdf.Tests) needs no corpus. It registers an
engraver and proves that what a renderer hands back becomes a picture in a real
PDF. The picture is BUILT by the test rather than committed - TestPng.Build
writes a valid PNG from first principles - so the repository carries no binary
fixture and so the claim being tested is that Html2Pdf really decodes it.

TexinfoPdfRendererTests (Texinfo2Pdf.Tests) covers the composition library's
own API from source written for it: both workflows, the options reaching both
stages, the metadata fill-in and its undo, the stage-tagged warning merge, and
the caller mistakes that are meant to throw. Two of its tests work from a
deliberately LONG document, because in a short manual every page break is a
chapter starting and no change to the stylesheet can move the page count -
which is what makes a stylesheet assertion look broken when it is not.

PdfFeaturePassThroughTests (Texinfo2Pdf.Tests) proves the live Options.Html
pass-through, the structured PdfItems, the TexinfoPdfFonts forwarding and the
SVG/WebP picture path - including the "image.svg.nativemissing" behaviour.

LibraryPackagingSmoke (both test projects) checks that the assembly loads,
carries the date-stamped version, exposes its internals to the test assembly,
and - for Texinfo2Pdf - that the public surface is exactly the five documented
types and that both dependencies flow through.

DocumentInvariants.cs (Texinfo2Html.Tests) is a helper, not a test class: the
structural rules every parsed document must obey, asserted by both the unit
tests and the corpus gate.

Fixtures committed to this repository must be original work written for the
test, or come from an explicitly MIT, CC0 or public-domain source listed in
THIRD-PARTY-NOTICES.txt. Nothing GFDL, nothing GPL.


PACKAGING AND PUBLISHING
========================

Both library csproj files carry GeneratePackageOnBuild=true, so a plain build
of a library project produces its .nupkg; there is no separate pack driver or
script. Pack from the repository root with

    dotnet pack src/CodeBrix.Texinfo2Html/CodeBrix.Texinfo2Html.csproj -c Release
    dotnet pack src/CodeBrix.Texinfo2Pdf/CodeBrix.Texinfo2Pdf.csproj -c Release

and publish the two packages together: Texinfo2Pdf's dependency on
Texinfo2Html is resolved at pack time from the ProjectReference to the version
computed in the same build.

VERSIONING: do not hardcode a literal <Version> in a packable csproj. Both
library csproj files carry the family's canonical date-stamped version block,
which computes 1.<years-since-2026>.<day-of-year>.<minute-of-day> from UtcNow
at build time. Leave that block alone. Consequences worth knowing: every build
produces a NEW version; two builds in the same UTC minute produce the SAME
version (so do not publish two packages from within one minute); this is not
SemVer, so major/minor do not signal API compatibility; to re-baseline the
minor number change _VersionBaseYear in the csproj.

WHAT SHIPS IN EACH NUPKG (from the csproj None/Pack items):

    CodeBrix.Texinfo2Html.MitLicenseForever
        icon-codebrix-128.png, README.md, THIRD-PARTY-NOTICES.txt (repo root)
        AGENT-README.txt  <- the REPOSITORY-ROOT file (Texinfo2Html guide)

    CodeBrix.Texinfo2Pdf.MitLicenseForever
        icon-codebrix-128.png, README.md, THIRD-PARTY-NOTICES.txt (repo root)
        AGENT-README.txt  <- src/CodeBrix.Texinfo2Pdf/AGENT-README.txt
                             (Texinfo2Pdf guide), packed under the same name

So the root AGENT-README.txt must stay the Texinfo2Html consumer guide and
src/CodeBrix.Texinfo2Pdf/AGENT-README.txt the Texinfo2Pdf one; each package
carries exactly the guide for itself. README.md is shared and is the nuget.org
readme for both. PackageRequireLicenseAcceptance is true on both.

The AI-agent pointer stubs at the repository root (AGENTS.md, CLAUDE.md,
.clinerules, .cursorrules, .cursor/rules/agent-readme.mdc, .windsurfrules,
.github/copilot-instructions.md, .junie/guidelines.md) all point at
README-INDEX.txt, which maps to the AGENT-README files. Keep README-INDEX.txt
in step when a README file is added or moved.


PROVENANCE AND VENDORED SOURCES
===============================

There are no vendored sources. CodeBrix.Texinfo2Html and CodeBrix.Texinfo2Pdf
are original implementations that read the Texinfo file format; they are not
derived from, and contain no code from, the GNU Texinfo project, texi2any,
lilypond-book or any other Texinfo implementation. "Texinfo" is used only to
name the file format; no affiliation with the GNU Project is claimed.

CodeBrix.PdfDocCreate.Html2Pdf is consumed as a NuGet package, not copied in;
it ships its own license and notices. THIRD-PARTY-NOTICES.txt records all of
this and is where any future third-party fixture or code must be listed.

The lilypond-book snippet option vocabulary and the "% begin verbatim" marker
convention were MEASURED from the English LilyPond documentation set (read
locally, never committed), not copied from lilypond-book's source.


CODING CONVENTIONS
==================

These are CodeBrix family-wide rules. They are not negotiable per-file, and the
build is configured so that violating most of them produces a warning.

  * Target framework is net10.0 only. No multi-targeting.
  * Nullable reference types are OFF. Never write a '?' on a reference type -
    no string?, no MyClass?, no object?. Value-type nullables (int?, bool?,
    DateOnly?, MyEnum?) are fine, because those are Nullable<T>. Never use the
    null-forgiveness '!' operator, and never add '#nullable enable'.
  * ImplicitUsings are OFF and there are no global usings. Every file declares
    its own using directives.
  * Namespaces are file-scoped ('namespace CodeBrix.Texinfo2Html.Model;').
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
  * Tests are xUnit v3 with SilverAssertions (see TESTING for the naming and
    body conventions).
  * The copyright string is the fixed literal
    'Copyright (c) 2026 Jeremy Ellis and contributors'.
  * Source files live in sub-folders with matching sub-namespaces; only the
    public entry-point types sit at a project root (see ARCHITECTURE).
  * Warnings, never exceptions, for anything found in a document. New
    degradations get a TexinfoWarningCategory that already exists unless a
    genuinely new kind of trouble appears; the category word is a consumer
    contract, so adding one is an API change to document in AGENT-README.txt.


NOTES
=====

  * Consumer-facing behaviour that changes must be reflected in the
    AGENT-README of the package it belongs to; behaviour of the Texinfo stage
    that Texinfo2Pdf merely forwards is documented ONCE, in the root
    AGENT-README.txt, and referred to from the Texinfo2Pdf one.
  * The upstream UI platform's name must not appear in any documentation file in this
    repository.
  * The TestResults/ folder at the root is created locally by "dotnet test"
    and holds nothing that belongs in source control.
  * The Texinfo2Pdf test project's SkiaSharp native-assets reference and the
    commented-out one in src/CodeBrix.Texinfo2Pdf/CodeBrix.Texinfo2Pdf.csproj
    are the two places a future maintainer is most likely to "helpfully"
    change; both carry comments explaining why they must stay as they are.

================================================================================
