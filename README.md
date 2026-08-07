# CodeBrix.Texinfo

CodeBrix.Texinfo is a pair of fully managed, cross-platform .NET libraries that turn GNU Texinfo documentation into nicely-formatted PDF documents.
`CodeBrix.Texinfo2Html` reads a Texinfo source file and renders it into HTML and CSS written specifically for PDF generation, and `CodeBrix.Texinfo2Pdf` takes that markup the rest of the way and produces the finished PDF using `CodeBrix.PdfDocCreate.Html2Pdf`.
Both libraries read standard Texinfo (`.texi`) files as well as the `.tely` Texinfo dialect produced by LilyPond and CodeBrix.LilyPort.
They are provided as .NET 10 libraries and the associated `CodeBrix.Texinfo2Html.MitLicenseForever` and `CodeBrix.Texinfo2Pdf.MitLicenseForever` NuGet packages.

CodeBrix.Texinfo supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Project status

This repository is **under construction**, and the two libraries are at different stages.

`CodeBrix.Texinfo2Html` renders Texinfo to HTML and CSS end to end and has a public API — see the sample below. Cross references render as text rather than as links, indices are collected but not yet printed, and `@lilypond` snippets are emitted as their source; those are the next pieces of work.

`CodeBrix.Texinfo2Pdf` has no public API yet. Until it does, render to HTML and CSS with `CodeBrix.Texinfo2Html` and hand the result to `CodeBrix.PdfDocCreate.Html2Pdf` yourself, as the sample below shows.

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

Note that the NuGet package ids carry the `.MitLicenseForever` suffix but the assemblies and namespaces do not — they are simply `CodeBrix.Texinfo2Html` and `CodeBrix.Texinfo2Pdf`. The suffix is a CodeBrix family convention that records the license the packages will always be published under.

## Sample Code

Render a Texinfo manual to an HTML/CSS pair, then to a PDF:

```csharp
using CodeBrix.PdfDocCreate.Html2Pdf;
using CodeBrix.Texinfo2Html;

var renderer = new TexinfoHtmlRenderer();
var result = renderer.GenerateFromFile("manual.texi");

//manual.html plus manual.css, ready to render or to edit by hand
var htmlPath = result.WriteToDirectory("out");

new HtmlPdfRenderer().RenderFile(htmlPath, "out/manual.pdf");

foreach (var warning in result.Warnings.Messages) { Console.WriteLine(warning); }
```

Restyle the intermediate before it becomes a PDF:

```csharp
var result = new TexinfoHtmlRenderer().GenerateFromFile("manual.texi");
var myCss = result.Css.Replace("#111111", "#000033");   //or replace it wholesale
var html = result.ToHtmlDocument(myCss);

new HtmlPdfRenderer().RenderHtml(html, "out/manual.pdf", result.BaseDirectory);
```

Nothing about a document's contents throws: unsupported, malformed or missing constructs degrade to the nearest readable thing and are reported in `result.Warnings`.

## License

The project is licensed under the MIT License. see: https://en.wikipedia.org/wiki/MIT_License
