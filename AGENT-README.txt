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

This repository is currently a SCAFFOLD. The solution, both library projects,
both test projects, the NuGet packaging metadata and the family documentation
files are all in place and the solution builds clean, but NEITHER LIBRARY
EXPOSES A PUBLIC API YET. The Texinfo parsing and rendering functionality is
still to be written.

Consequences for an agent working in this repository:

  * Do not assume any type described below exists. There is no CORE API
    REFERENCE section yet because there is no public API to document.
  * The two src projects contain only InternalsVisibleTo.cs. When you add the
    first real source files, organize them into sub-folders and matching
    sub-namespaces from the outset (see ARCHITECTURE below) rather than piling
    them at the project root.
  * The two test projects each contain a single LibraryPackagingSmoke.cs that
    exercises assembly identity, the date-stamped version stamping, the
    InternalsVisibleTo wiring, and - for CodeBrix.Texinfo2Pdf - that the
    CodeBrix.Texinfo2Html and CodeBrix.PdfDocCreate.Html2Pdf assemblies flow
    through to the test output. Those tests exist so the suite is green and
    meaningful while the libraries are empty; they are not a substitute for
    real functional tests and should be joined by real tests, not replaced by
    them, as functionality lands.
  * Update this file as the public API appears. Document what is true, never
    what is planned.


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

Sub-namespaces will be added underneath these two roots as the libraries are
built out. There is no separate CodeBrix.Texinfo namespace and no project named
CodeBrix.Texinfo - that name belongs to the repository and to the solution file
only.


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

When source files are added, organize them into sub-folders with matching
sub-namespaces from day one - Models/, Enumerations/, Extensions/, Internal/
and so on - and keep only the entry-point types and the library's exception type
at the project root.


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

Run the whole suite from the repository root:

    dotnet test CodeBrix.Texinfo.slnx


================================================================================
