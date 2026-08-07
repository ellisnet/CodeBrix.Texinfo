namespace CodeBrix.Texinfo2Html.Emit;

/// <summary>
/// The stylesheet the generated markup is written against: a printed-manual look modelled on the
/// way Texinfo documents have always appeared on paper - serif body text, a plain numbered heading
/// hierarchy, boxed examples, and a table of contents without page numbers, which the HTML
/// intermediate has no way of knowing.
/// </summary>
/// <remarks>
/// Every rule here stays inside the CSS dialect CodeBrix.PdfDocCreate.Html2Pdf implements, so
/// nothing in it is silently dropped at render time. It is written at author level, which means a
/// consumer can append to it through the options or replace it outright and still have a document
/// that renders.
/// </remarks>
internal static class DefaultStylesheet
{
    /// <summary>The stylesheet text.</summary>
    public const string Css = @"/* CodeBrix.Texinfo2Html - default print stylesheet */

html { font-family: serif; font-size: 10.5pt; color: #111111; line-height: 1.5; }
body { margin: 0; }

/* ----- sectioning ------------------------------------------------------- */

h1, h2, h3, h4, h5, h6 { font-family: serif; font-weight: bold; color: #111111; }
h1 { font-size: 1.9em; margin: 0.2em 0 0.6em 0; }
h2 { font-size: 1.5em; margin: 1.2em 0 0.5em 0; }
h3 { font-size: 1.22em; margin: 1.05em 0 0.4em 0; }
h4 { font-size: 1.08em; margin: 0.95em 0 0.35em 0; }
h5 { font-size: 1em; margin: 0.9em 0 0.3em 0; }
h6 { font-size: 0.95em; margin: 0.9em 0 0.3em 0; }
.texinfo-secnum { font-weight: bold; }

/* @heading and friends print a heading without creating structure, so they are
   styled to match the sectioning commands but never enter the PDF outline. */
.texinfo-heading-1 { font-size: 1.5em; font-weight: bold; margin: 1.2em 0 0.5em 0; }
.texinfo-heading-2 { font-size: 1.22em; font-weight: bold; margin: 1.05em 0 0.4em 0; }
.texinfo-heading-3 { font-size: 1.08em; font-weight: bold; margin: 0.95em 0 0.35em 0; }
.texinfo-heading-4 { font-size: 1em; font-weight: bold; margin: 0.9em 0 0.3em 0; }
.texinfo-heading-5 { font-size: 0.95em; font-weight: bold; margin: 0.9em 0 0.3em 0; }

/* ----- running text ----------------------------------------------------- */

p { margin: 0 0 0.62em 0; text-align: left; }
p.texinfo-noindent { text-indent: 0; }
p.texinfo-center { text-align: center; }
a { color: #14417a; text-decoration: none; }

/* ----- title page ------------------------------------------------------- */

.texinfo-titlepage { text-align: center; margin: 0 0 1.5em 0; page-break-after: always; }
.texinfo-title { font-size: 2.4em; font-weight: bold; margin: 1.5em 0 0.2em 0; }
.texinfo-subtitle { font-size: 1.35em; margin: 0 0 0.6em 0; }
.texinfo-titlefont { font-size: 1.6em; font-weight: bold; }
.texinfo-author { font-size: 1.1em; font-style: italic; margin: 0.8em 0 0 0; }

/* ----- table of contents ------------------------------------------------ */

.texinfo-contents-heading { font-size: 1.5em; font-weight: bold; margin: 0.6em 0 0.6em 0; }
.texinfo-toc-0 { margin: 0.35em 0 0.1em 0; font-weight: bold; }
.texinfo-toc-1 { margin: 0 0 0.05em 1.4em; }
.texinfo-toc-2 { margin: 0 0 0.05em 2.8em; }
.texinfo-toc-3 { margin: 0 0 0.05em 4.2em; }
.texinfo-toc-4 { margin: 0 0 0.05em 5.6em; }

/* ----- preformatted environments ---------------------------------------- */

pre { font-family: monospace; font-size: 0.84em; line-height: 1.38; white-space: pre;
      margin: 0.55em 0 0.75em 0; padding: 6pt 8pt; }
pre.texinfo-example { background-color: #f5f5f2; border: 0.6pt solid #d7d7d0; }
pre.texinfo-smallexample { font-size: 0.76em; background-color: #f5f5f2;
                           border: 0.6pt solid #d7d7d0; }
pre.texinfo-display { padding: 0 0 0 12pt; }
pre.texinfo-format { padding: 0; }
pre.texinfo-verbatim { background-color: #f5f5f2; border: 0.6pt solid #d7d7d0; }
pre.texinfo-lilypond { background-color: #f2f5f2; border: 0.6pt solid #cfd9cf; }

code, samp, kbd { font-family: monospace; font-size: 0.9em; }
kbd { font-weight: bold; }
.texinfo-t { font-family: monospace; font-size: 0.9em; }
.texinfo-r { font-family: serif; }
.texinfo-sansserif { font-family: sans-serif; }
.texinfo-sc { text-transform: uppercase; font-size: 0.85em; }
.texinfo-var { font-style: italic; }
.texinfo-math { font-style: italic; }
.texinfo-key { font-family: monospace; font-size: 0.88em; font-weight: bold; }
.texinfo-url { font-family: monospace; font-size: 0.9em; }

/* ----- quoted and boxed environments ------------------------------------ */

/* Quotations and indented blocks are indented, never boxed. The border and the grey
   text are cleared deliberately, not by omission: Html2Pdf's built-in stylesheet gives
   every blockquote a left rule, and a bordered container is laid out as a box, which
   cannot hold a table - and Texinfo manuals do put @multitable inside @quotation. */
blockquote { margin: 0.5em 2.2em 0.75em 2.2em; padding: 0; border: none; color: #111111; }
blockquote.texinfo-quotation { margin: 0.5em 2.2em 0.75em 2.2em; }
.texinfo-quotation-label { font-weight: bold; margin: 0 0 0.25em 0; }
.texinfo-cartouche { border: 0.9pt solid #9a9a90; padding: 7pt 9pt; margin: 0.6em 0 0.85em 0; }
.texinfo-raggedright { text-align: left; }
.texinfo-flushleft { text-align: left; }
.texinfo-flushright { text-align: right; }

/* ----- lists and tables ------------------------------------------------- */

ul, ol { margin: 0 0 0.7em 0; }
li { margin: 0 0 0.2em 0; }
dl.texinfo-table { margin: 0.4em 0 0.75em 0; }
dt { font-weight: bold; margin: 0.35em 0 0.1em 0; }
dd { margin: 0 0 0.25em 1.6em; }
table.texinfo-multitable { margin: 0.5em 0 0.85em 0; }
table.texinfo-multitable th { font-weight: bold; background-color: #eeeee8;
                              border: 0.5pt solid #c6c6be; padding: 3pt 5pt; }
table.texinfo-multitable td { border: 0.5pt solid #c6c6be; padding: 3pt 5pt;
                              vertical-align: top; }

/* ----- footnotes -------------------------------------------------------- */

.texinfo-footnotes { margin: 1.4em 0 0 0; font-size: 0.9em; }
.texinfo-footnotes-heading { font-weight: bold; font-size: 1.05em; margin: 0 0 0.4em 0; }
.texinfo-footnote-item { margin: 0 0 0.3em 0; }
.texinfo-footnote-ref { font-size: 0.75em; }

/* ----- odds and ends ---------------------------------------------------- */

.texinfo-anchor { font-size: 1pt; line-height: 1; margin: 0; }
.texinfo-page-break { page-break-before: always; }
.texinfo-blank { margin: 0 0 0.7em 0; }
.texinfo-image { margin: 0.45em 0; }
.texinfo-missing-image { font-style: italic; color: #6a6a62; font-size: 0.9em; }
.texinfo-unknown { margin: 0 0 0.6em 0; }
hr { border-top: 0.6pt solid #c6c6be; margin: 1em 0; }
";
}
