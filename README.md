# CodeBrix.Texinfo

CodeBrix.Texinfo is a pair of fully managed, cross-platform .NET libraries that turn GNU Texinfo documentation into nicely-formatted PDF documents.
`CodeBrix.Texinfo2Html` reads a Texinfo source file and renders it into HTML and CSS written specifically for PDF generation, and `CodeBrix.Texinfo2Pdf` takes that markup the rest of the way and produces the finished PDF using `CodeBrix.PdfDocCreate.Html2Pdf`.
Both libraries read standard Texinfo (`.texi`) files as well as the `.tely` Texinfo dialect produced by LilyPond and CodeBrix.LilyPort.
They are provided as .NET 10 libraries and the associated `CodeBrix.Texinfo2Html.MitLicenseForever` and `CodeBrix.Texinfo2Pdf.MitLicenseForever` NuGet packages.

CodeBrix.Texinfo supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## ⚠️ Important for Linux: SVG rendering needs a SkiaSharp native-assets package

**If your application runs on Linux and converts Texinfo documents that embed SVG pictures**
through `CodeBrix.Texinfo2Pdf` - or feeds `CodeBrix.Texinfo2Html` output to
`CodeBrix.PdfDocCreate.Html2Pdf` yourself - your application must reference **one** of these two
NuGet packages itself:

```
dotnet add package SkiaSharp.NativeAssets.Linux
```

**or**

```
dotnet add package SkiaSharp.NativeAssets.Linux.NoDependencies
```

**Either package works equally well - neither is recommended over the other.** Reference exactly
one, whichever suits your application. **If your application already references one of them for its
own reasons, keep that one** - nothing needs to change, and you should not swap it for the other,
and you certainly do not need both.

The two differ only in how the native library obtains font services: `SkiaSharp.NativeAssets.Linux`
links against the system `libfontconfig`, while `SkiaSharp.NativeAssets.Linux.NoDependencies` is
self-contained. That difference does not affect this conversion, which never consults system fonts,
so the choice is yours to make on your own deployment grounds.

**Windows and macOS require nothing extra** - SkiaSharp supplies those native binaries through its
own package. This requirement applies to Linux only, and only when a document actually contains SVG
content.

**`CodeBrix.Texinfo2Html` on its own needs nothing.** It only emits HTML and CSS and never
rasterizes anything, so it never touches SkiaSharp. The requirement arrives with the PDF stage.

**Why isn't this just a package dependency?** Two mutually exclusive Linux native-assets variants
exist, and only the consuming application can decide which one it wants. `CodeBrix.Texinfo2Pdf`
therefore does not declare either one - and neither does `CodeBrix.PdfDocCreate.Html2Pdf`
underneath it - because declaring one would force that choice on every consumer and conflict with
applications that already reference the other. So the choice is deliberately left to you.

**What happens if it is missing?** Nothing crashes and nothing throws. SVG pictures are skipped and
the rest of the document renders normally into a complete PDF. The skip is reported through the
conversion's collected warnings, so if SVG content is unexpectedly absent from your PDF, inspect
`result.Warnings`:

```csharp
var result = new TexinfoPdfRenderer().RenderFile("manual.texi", "out/manual.pdf");

//the guidance message, tagged [pdf] - or use result.Warnings.PdfMessages for it untagged
foreach (var warning in result.Warnings.Messages) { Console.WriteLine(warning); }

//or test for it exactly, by its stable code
bool svgSkipped = result.Warnings.PdfItems.Any(i => i.Code == "image.svg.nativemissing");
```

Every SVG in the document fails for the same one environmental reason, so they collapse into a
single warning whose `Occurrences` count says how many pictures were skipped. The message itself
names `CodeBrix.PdfDocCreate.Html2Pdf` rather than `CodeBrix.Texinfo2Pdf`, because that is the
library underneath doing the rasterizing - the packages to add are the same two either way.

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

On Linux, an application using this package to convert documents that contain SVG pictures must also reference `SkiaSharp.NativeAssets.Linux` or `SkiaSharp.NativeAssets.Linux.NoDependencies` itself — either one, never both. That dependency is real but undeclared, deliberately; see the important note near the top of this document.

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

If all you want is the HTML and CSS, install `CodeBrix.Texinfo2Html` on its own and use `TexinfoHtmlRenderer` directly — it has no dependencies at all beyond .NET, on any operating system:

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
