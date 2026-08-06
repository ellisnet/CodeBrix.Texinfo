# CodeBrix.Texinfo

CodeBrix.Texinfo is a pair of fully managed, cross-platform .NET libraries that turn GNU Texinfo documentation into nicely-formatted PDF documents.
`CodeBrix.Texinfo2Html` reads a Texinfo source file and renders it into HTML and CSS written specifically for PDF generation, and `CodeBrix.Texinfo2Pdf` takes that markup the rest of the way and produces the finished PDF using `CodeBrix.PdfDocCreate.Html2Pdf`.
Both libraries read standard Texinfo (`.texi`) files as well as the `.tely` Texinfo dialect produced by LilyPond and CodeBrix.LilyPort.
They are provided as .NET 10 libraries and the associated `CodeBrix.Texinfo2Html.MitLicenseForever` and `CodeBrix.Texinfo2Pdf.MitLicenseForever` NuGet packages.

CodeBrix.Texinfo supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Project status

This repository is currently a **scaffold**. The solution, both library projects, both test projects, the NuGet packaging metadata and the family documentation files are all in place, but neither library exposes a public API yet — the Texinfo parsing and rendering functionality is still to be written. The sections below describe what each library is intended to do.

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

Sample code will be added here once the public API of each library exists.

## License

The project is licensed under the MIT License. see: https://en.wikipedia.org/wiki/MIT_License
