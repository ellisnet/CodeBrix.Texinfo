# CodeBrix.Texinfo

CodeBrix.Texinfo is a pair of fully managed, cross-platform .NET libraries that turn GNU Texinfo documentation into nicely-formatted PDF documents.
`CodeBrix.Texinfo2Html` reads a Texinfo source file and renders it into HTML and CSS written specifically for PDF generation, and `CodeBrix.Texinfo2Pdf` takes that markup the rest of the way and produces the finished PDF using `CodeBrix.PdfDocCreate.Html2Pdf`.
Both libraries read standard Texinfo (`.texi`) files as well as the `.tely` Texinfo dialect produced by LilyPond and CodeBrix.LilyPort.
They are provided as .NET 10 libraries and the associated `CodeBrix.Texinfo2Html.MitLicenseForever` and `CodeBrix.Texinfo2Pdf.MitLicenseForever` NuGet packages.

CodeBrix.Texinfo supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Project status

Both libraries are complete and have public APIs — see the samples below.

`CodeBrix.Texinfo2Html` renders Texinfo to HTML and CSS end to end. It resolves cross references to real links, prints indices, places footnotes at the end of the chapter they belong to, carries a document's pictures along with it, and reads the LilyPond music environments of a `.tely` document so that a renderer you register can engrave them. It expands a document's own macros — `@macro`, `@rmacro` and `@linemacro`, including the ones a manual uses to define new definition commands — which is what lets a real manual work at all. It covers the general-Texinfo commands a GNU manual is built from as well: the `@deffn` definition family, numbered floats and their captions, the full index set including the indices a document defines for itself, and the accent and glyph commands.

Anything a document uses that these libraries do not implement becomes a warning and the closest readable degradation, never an exception — so the way to find out whether your manual works is to render it and read `result.Warnings`. The whole English LilyPond documentation set renders, as do the GNU Texinfo manual and the GNU Make manual.

`CodeBrix.Texinfo2Pdf` performs the whole conversion in one call, and is the package to install if what you want is a PDF.

## The two libraries

### CodeBrix.Texinfo2Html

Takes a standard Texinfo (`.texi`) file — or a LilyPond/CodeBrix.LilyPort `.tely` file — and renders it into HTML and CSS. The markup it emits is written for PDF generation rather than for the browser: it stays inside the documented HTML and CSS subset that `CodeBrix.PdfDocCreate.Html2Pdf` understands, so the output is ready to be fed straight into that library to produce a nicely-formatted PDF.

NuGet package: `CodeBrix.Texinfo2Html.MitLicenseForever`

    dotnet add package CodeBrix.Texinfo2Html.MitLicenseForever

### CodeBrix.Texinfo2Pdf

A convenience library that performs the whole conversion in one step. It renders the Texinfo source to HTML and CSS with `CodeBrix.Texinfo2Html`, then hands that markup to `CodeBrix.PdfDocCreate.Html2Pdf` to produce the finished PDF document. Use this package when you want a PDF; use `CodeBrix.Texinfo2Html` on its own when you want the intermediate HTML and CSS, or when you want to post-process the markup before it is rendered.

`CodeBrix.Texinfo2Pdf.MitLicenseForever` depends on `CodeBrix.Texinfo2Html.MitLicenseForever` and on `CodeBrix.PdfDocCreate.Html2Pdf.MitLicenseForever`, so installing it brings the whole conversion chain with it.

NuGet package: `CodeBrix.Texinfo2Pdf.MitLicenseForever`

    dotnet add package CodeBrix.Texinfo2Pdf.MitLicenseForever

## CodeBrix.Texinfo supports:

* Standard GNU Texinfo (`.texi`) source files
* The `.tely` Texinfo dialect produced by LilyPond and CodeBrix.LilyPort
* Rendering Texinfo to HTML and CSS that is ready for PDF generation
* Producing a finished, nicely-formatted PDF in a single call
* A seam for engraving `@lilypond` music, so a `.tely` manual can print its music as music

Note that the NuGet package ids carry the `.MitLicenseForever` suffix but the assemblies and namespaces do not — they are simply `CodeBrix.Texinfo2Html` and `CodeBrix.Texinfo2Pdf`. The suffix is a CodeBrix family convention that records the license the packages will always be published under.

## Sample Code

Turn a Texinfo manual into a PDF:

```csharp
using CodeBrix.Texinfo2Pdf;

var renderer = new TexinfoPdfRenderer();
var result = renderer.RenderFile("manual.texi", "out/manual.pdf");

Console.WriteLine($"{result.PageCount} pages, {result.Warnings.Count} warnings");
foreach (var warning in result.Warnings.Messages) { Console.WriteLine(warning); }
```

The output directory is created if it is not there, the document's pictures are carried along without your having to place them, and what lands in `out` is one PDF and nothing else. Leave the output path off entirely and the PDF is written beside the source file.

Set the page up, or give the document a running header and footer of your own:

```csharp
var renderer = new TexinfoPdfRenderer();
renderer.Options.Html.SetPageSize("a4");
renderer.Options.Html.FooterText = "Page {page} of {pages}";
renderer.Options.Texinfo.PredefinedValues["version"] = "2.1";   //as though @set version 2.1

var pdf = renderer.RenderFileToBytes("manual.texi");            //or RenderFile, to disk
```

`Options.Texinfo` and `Options.Html` are the real option objects of the two libraries underneath, so anything either of them can do is reachable without a second package reference.

Restyle the intermediate before it becomes a PDF:

```csharp
var renderer = new TexinfoPdfRenderer();
var html = renderer.GenerateHtmlFromFile("manual.texi");

//html.BodyHtml, html.Css, html.Title and the document's picture list are all here
var myCss = html.Css.Replace("#111111", "#000033");   //or replace it wholesale

renderer.RenderHtml(html, "out/manual.pdf", myCss);
```

Or write the pair out, edit the files by hand, and come back in:

```csharp
var path = html.WriteToDirectory("work");   //work/manual.html plus work/manual.css
//...edit them...
renderer.RenderHtmlFile(path, "out/manual.pdf");
```

Warnings from both halves of the conversion arrive in one list, each tagged with the half that produced it, and split out as `Warnings.TexinfoMessages` and `Warnings.PdfMessages` when you want to tell a problem in the source from a problem in the typesetting.

If all you want is the HTML and CSS, install `CodeBrix.Texinfo2Html` on its own and use `TexinfoHtmlRenderer` directly — it has no dependencies at all beyond .NET:

```csharp
using CodeBrix.Texinfo2Html;

var result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
var htmlPath = result.WriteToDirectory("out");   //manual.html, manual.css, and the pictures
```

Engrave the music of a `.tely` manual. Without a renderer registered, every `@lilypond` snippet is shown as its source text — this library will not take on a dependency on LilyPond, so it defines the seam and leaves the engraving to a consumer who already has an engraver:

```csharp
public sealed class MyEngraver : ILilypondSnippetRenderer
{
    public LilypondSnippetResult Render(LilypondSnippet snippet)
    {
        //snippet.Source (or snippet.FilePath), plus snippet.Options.Relative,
        //.Fragment, .RaggedRight, .LineWidth, .StaffSize and the rest
        byte[] png = MyLilypond.Engrave(snippet);
        return LilypondSnippetResult.FromContent(png, "png");
    }
}

var renderer = new TexinfoPdfRenderer();
renderer.Options.Texinfo.SnippetRenderer = new MyEngraver();

//the engraved pictures travel with the document, exactly as @image pictures do
renderer.RenderFile("notation.tely", "out/notation.pdf");
```

An identical snippet is engraved once and the picture reused, so a manual that repeats a snippet does not pay for it twice. Return `LilypondSnippetResult.NotRendered` to decline one quietly, or `LilypondSnippetResult.Failed("...")` to report why it could not be done.

Nothing about a document's contents throws: unsupported, malformed or missing constructs degrade to the nearest readable thing and are reported in `result.Warnings`. That includes an exception escaping your own snippet renderer.

## License

The project is licensed under the MIT License. see: https://en.wikipedia.org/wiki/MIT_License
