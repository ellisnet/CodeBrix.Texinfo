================================================================================
EXTRAS-README: CodeBrix.Texinfo
Samples, tools and other content in this repository that is not part of a NuGet package
================================================================================

There are no samples, tools, demo applications or documentation folders in this
repository. There is no samples/, tools/ or docs/ folder; the only content
outside the two packaged library projects is the tests/ folder.


TESTS (the only non-package content)
------------------------------------

    tests/CodeBrix.Texinfo2Html.Tests/     xUnit v3 tests for CodeBrix.Texinfo2Html
    tests/CodeBrix.Texinfo2Pdf.Tests/      xUnit v3 tests for CodeBrix.Texinfo2Pdf

Run with "dotnet test --solution CodeBrix.Texinfo.slnx" from the repository
root; see MAINTAINER-README.txt (TESTING) for the runner requirements and the
suite-by-suite description. For a consumer, the test files double as working
examples of each package's API; the AGENT-README files link to them under
WORKING EXAMPLES ON GITHUB.


OPTIONAL TEST DATA (external, never committed)
----------------------------------------------

Several test suites run against the English LilyPond documentation set (eight
Texinfo manuals, about 110,000 lines) and the GNU Texinfo and GNU Make manuals.
These are GFDL-licensed and are NOT in this repository: the tests read them
from ~/GitHome/lilypond/Documentation on the local machine and skip cleanly
when that folder is absent. Everything else in the test projects is
self-contained - every committed fixture is an original document written for
its test, and the one binary picture a test needs is generated at run time
(tests/CodeBrix.Texinfo2Pdf.Tests/TestPng.cs).

The end-to-end gate leaves the PDFs it builds in <temp>/codebrix-texinfo-gate
so they can be inspected afterwards; that folder is a local by-product, not
repository content.

================================================================================
