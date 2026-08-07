using System;
using System.IO;
using System.Linq;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// Tests for the public rendering API and, through it, the semantic passes and the HTML emitter.
/// Every fixture in this file is original Texinfo written for the test, so the repository stays
/// free of documentation licensed under terms the library cannot ship.
/// </summary>
public class TexinfoHtmlRendererTests
{
    private static TexinfoHtmlResult Render(string source)
        => new TexinfoHtmlRenderer().Generate(source);

    private static string Body(string source) => Render(source).BodyHtml;

    // ----- document shape --------------------------------------------------------------------

    [Fact]
    public void Generate_produces_a_document_that_links_to_a_stylesheet_file()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@settitle Handbook\n@chapter One\nText.\n");

        //Assert
        result.Title.Should().Be("Handbook");
        result.CssFileName.Should().Be("texinfo.css");
        result.Html.Contains("<link rel=\"stylesheet\" href=\"texinfo.css\">").Should().BeTrue();
        result.Html.Contains("<style>").Should().BeFalse();
        result.Css.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_embeds_the_stylesheet_when_a_single_file_is_asked_for()
    {
        //Arrange
        TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
        renderer.Options.EmitSingleFile = true;

        //Act
        TexinfoHtmlResult result = renderer.Generate("@chapter One\nText.\n");

        //Assert
        result.Html.Contains("<style>").Should().BeTrue();
        result.Html.Contains("<link rel=\"stylesheet\"").Should().BeFalse();
        //The stylesheet stays available on its own even when it was embedded.
        result.Css.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_escapes_characters_that_would_otherwise_be_markup()
    {
        //Arrange + Act
        string body = Body("A < B & C > D\n");

        //Assert
        body.Contains("A &lt; B &amp; C &gt; D").Should().BeTrue();
    }

    [Fact]
    public void Generate_appends_the_extra_stylesheet_after_the_built_in_one()
    {
        //Arrange
        TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
        renderer.Options.ExtraCss = "p { color: #ff0000; }";

        //Act
        TexinfoHtmlResult result = renderer.Generate("Text.\n");

        //Assert
        result.Css.IndexOf("p { color: #ff0000; }", StringComparison.Ordinal)
            .Should().BeGreaterThan(result.Css.IndexOf("html {", StringComparison.Ordinal));
    }

    [Fact]
    public void ToHtmlDocument_rebuilds_the_document_around_a_replacement_stylesheet()
    {
        //Arrange
        TexinfoHtmlResult result = Render("@settitle Handbook\nText.\n");

        //Act
        string document = result.ToHtmlDocument("p { color: #00ff00; }");

        //Assert
        document.Contains("p { color: #00ff00; }").Should().BeTrue();
        document.Contains("<title>Handbook</title>").Should().BeTrue();
        document.Contains("<link rel=\"stylesheet\"").Should().BeFalse();
    }

    [Fact]
    public void WriteToDirectory_writes_the_markup_and_the_stylesheet_beside_it()
    {
        //Arrange
        TexinfoHtmlResult result = Render("@settitle Handbook\n@chapter One\nText.\n");
        string directory = Directory.CreateTempSubdirectory("texinfo-write-").FullName;

        try
        {
            //Act
            string htmlPath = result.WriteToDirectory(directory, "manual");

            //Assert
            Path.GetFileName(htmlPath).Should().Be("manual.html");
            File.Exists(htmlPath).Should().BeTrue();
            File.Exists(Path.Combine(directory, "texinfo.css")).Should().BeTrue();
            File.ReadAllText(htmlPath).Contains("<h1").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WriteToDirectory_writes_only_the_markup_for_a_single_file_document()
    {
        //Arrange
        TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
        renderer.Options.EmitSingleFile = true;
        TexinfoHtmlResult result = renderer.Generate("@chapter One\nText.\n");
        string directory = Directory.CreateTempSubdirectory("texinfo-write-single-").FullName;

        try
        {
            //Act
            result.WriteToDirectory(directory, "manual");

            //Assert
            Directory.GetFiles(directory).Length.Should().Be(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GenerateFromFile_rejects_a_blank_path()
        => Assert.Throws<ArgumentException>(() => new TexinfoHtmlRenderer().GenerateFromFile("  "));

    [Fact]
    public void GenerateFromFile_reports_a_file_that_does_not_exist()
        => Assert.Throws<FileNotFoundException>(() =>
            new TexinfoHtmlRenderer().GenerateFromFile(
                Path.Combine(Path.GetTempPath(), "no-such-texinfo-file-8f21.texi")));

    [Fact]
    public void GenerateFromFile_names_the_stylesheet_after_the_source_file()
    {
        //Arrange
        string directory = Directory.CreateTempSubdirectory("texinfo-from-file-").FullName;
        string sourcePath = Path.Combine(directory, "handbook.texi");
        File.WriteAllText(sourcePath, "@settitle Handbook\n@chapter One\nText.\n");

        try
        {
            //Act
            TexinfoHtmlResult result = new TexinfoHtmlRenderer().GenerateFromFile(sourcePath);

            //Assert
            result.CssFileName.Should().Be("handbook.css");
            result.Html.Contains("href=\"handbook.css\"").Should().BeTrue();
            result.BaseDirectory.Should().Be(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ----- sectioning and numbering ----------------------------------------------------------

    [Fact]
    public void Generate_numbers_chapters_sections_and_appendices()
    {
        //Arrange
        const string source = "@chapter First\n@section Alpha\n@subsection Detail\n"
            + "@section Beta\n@chapter Second\n@appendix Extra\n@appendixsec Note\n";

        //Act
        string body = Body(source);

        //Assert
        body.Contains(">1</span> First").Should().BeTrue();
        body.Contains(">1.1</span> Alpha").Should().BeTrue();
        body.Contains(">1.1.1</span> Detail").Should().BeTrue();
        body.Contains(">1.2</span> Beta").Should().BeTrue();
        body.Contains(">2</span> Second").Should().BeTrue();
        body.Contains(">A</span> Extra").Should().BeTrue();
        body.Contains(">A.1</span> Note").Should().BeTrue();
    }

    [Fact]
    public void Generate_leaves_unnumbered_units_and_their_children_unnumbered()
    {
        //Arrange + Act
        string body = Body("@unnumbered Preface\n@section Inside\n@chapter Real\n");

        //Assert
        body.Contains("Preface").Should().BeTrue();
        body.Contains("texinfo-secnum\">1</span> Real").Should().BeTrue();
        //Nothing under an unnumbered chapter has a stem to be numbered from.
        body.Contains(">1.1</span> Inside").Should().BeFalse();
    }

    [Fact]
    public void Generate_omits_every_number_when_numbering_is_turned_off()
    {
        //Arrange
        TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
        renderer.Options.NumberSections = false;

        //Act
        string body = renderer.Generate("@chapter First\n@section Alpha\n").BodyHtml;

        //Assert
        body.Contains("texinfo-secnum").Should().BeFalse();
        body.Contains("First").Should().BeTrue();
    }

    [Fact]
    public void Generate_ranks_headings_by_their_depth_in_the_sectioning_tree()
    {
        //Arrange
        const string source = "@top Manual\n@chapter One\n@section Two\n@subsection Three\n"
            + "@subsubsection Four\n";

        //Act
        string body = Body(source);

        //Assert
        body.Contains("<h1 id=\"Manual\">").Should().BeTrue();
        body.Contains("<h2 ").Should().BeTrue();
        body.Contains("<h3 ").Should().BeTrue();
        body.Contains("<h4 ").Should().BeTrue();
        body.Contains("<h5 ").Should().BeTrue();
    }

    [Fact]
    public void Generate_identifies_a_section_by_the_node_that_introduced_it()
    {
        //Arrange + Act
        string body = Body("@node Getting Started\n@chapter Getting Started\nText.\n");

        //Assert
        body.Contains("id=\"Getting-Started\"").Should().BeTrue();
    }

    [Fact]
    public void Generate_keeps_identifiers_unique_when_two_titles_agree()
    {
        //Arrange + Act
        string body = Body("@chapter Notes\n@chapter Notes\n");

        //Assert
        body.Contains("id=\"Notes\"").Should().BeTrue();
        body.Contains("id=\"Notes-2\"").Should().BeTrue();
    }

    [Fact]
    public void Generate_renders_the_heading_family_without_putting_it_in_the_outline()
    {
        //Arrange + Act
        string body = Body("@chapter One\n@subheading Aside\nText.\n");

        //Assert
        //@subheading prints a heading but creates no structure, so it must not be an h-element.
        body.Contains("<p class=\"texinfo-heading-3\">Aside").Should().BeTrue();
        body.Contains("<h3").Should().BeFalse();
    }

    // ----- table of contents -----------------------------------------------------------------

    [Fact]
    public void Generate_builds_a_contents_of_links_to_every_section()
    {
        //Arrange
        const string source = "@top Manual\n@contents\n@node Start\n@chapter Start\n"
            + "@node Deeper\n@section Deeper\n";

        //Act
        string body = Body(source);

        //Assert
        body.Contains("texinfo-contents-heading\">Table of Contents").Should().BeTrue();
        body.Contains("<p class=\"texinfo-toc-0\"><a href=\"#Start\">1 Start</a>").Should().BeTrue();
        body.Contains("<p class=\"texinfo-toc-1\"><a href=\"#Deeper\">1.1 Deeper</a>").Should().BeTrue();
        //The topmost unit names the document itself and is left out of its own contents.
        body.Contains("#Manual").Should().BeFalse();
    }

    [Fact]
    public void Generate_limits_the_short_contents_to_chapter_level_units()
    {
        //Arrange + Act
        string body = Body("@shortcontents\n@chapter One\n@section Deep\n@chapter Two\n");

        //Assert
        body.Contains("Short Contents").Should().BeTrue();
        body.Contains(">1 One</a>").Should().BeTrue();
        body.Contains(">2 Two</a>").Should().BeTrue();
        body.Contains(">1.1 Deep</a>").Should().BeFalse();
    }

    // ----- block environments ----------------------------------------------------------------

    [Theory]
    [InlineData("example", "texinfo-example")]
    [InlineData("smallexample", "texinfo-smallexample")]
    [InlineData("lisp", "texinfo-example")]
    [InlineData("display", "texinfo-display")]
    [InlineData("format", "texinfo-format")]
    public void Generate_renders_a_preformatted_environment_as_a_pre_element(string command,
        string cssClass)
    {
        //Arrange + Act
        string body = Body($"@{command}\none  two\n@end {command}\n");

        //Assert
        body.Contains($"<pre class=\"{cssClass}\">").Should().BeTrue();
        //The two spaces are the point of a preformatted environment; they must survive.
        body.Contains("one  two").Should().BeTrue();
    }

    [Fact]
    public void Generate_never_indents_inside_a_preformatted_environment()
    {
        //Arrange + Act
        string body = Body("@itemize\n@item\n@example\nfirst\nsecond\n@end example\n@end itemize\n");

        //Assert
        //Indentation added for readability would become content inside a pre element.
        body.Contains(">first\nsecond\n</pre>").Should().BeTrue();
    }

    [Fact]
    public void Generate_renders_verbatim_content_without_interpreting_it()
    {
        //Arrange + Act
        string body = Body("@verbatim\n@code{not a command} <tag>\n@end verbatim\n");

        //Assert
        body.Contains("<pre class=\"texinfo-verbatim\">").Should().BeTrue();
        body.Contains("@code{not a command} &lt;tag&gt;").Should().BeTrue();
    }

    [Fact]
    public void Generate_renders_a_quotation_with_its_label()
    {
        //Arrange + Act
        string body = Body("@quotation Note\nMind the gap.\n@end quotation\n");

        //Assert
        body.Contains("<blockquote class=\"texinfo-quotation\">").Should().BeTrue();
        body.Contains("<p class=\"texinfo-quotation-label\">Note").Should().BeTrue();
        body.Contains("Mind the gap.").Should().BeTrue();
    }

    [Theory]
    [InlineData("cartouche", "div class=\"texinfo-cartouche\"")]
    [InlineData("indentedblock", "blockquote class=\"texinfo-indentedblock\"")]
    [InlineData("raggedright", "div class=\"texinfo-raggedright\"")]
    [InlineData("flushright", "div class=\"texinfo-flushright\"")]
    public void Generate_renders_the_remaining_block_environments(string command, string expected)
    {
        //Arrange + Act
        string body = Body($"@{command}\nInside.\n@end {command}\n");

        //Assert
        body.Contains("<" + expected + ">").Should().BeTrue();
        body.Contains("Inside.").Should().BeTrue();
    }

    [Fact]
    public void Generate_treats_a_group_as_transparent()
    {
        //Arrange + Act
        string body = Body("@group\nKept together.\n@end group\n");

        //Assert
        //@group only asks that its content not be split across a page; it adds no structure.
        body.Contains("Kept together.").Should().BeTrue();
        body.Contains("<div").Should().BeFalse();
    }

    [Fact]
    public void Generate_hoists_the_title_page_to_the_front_of_the_document()
    {
        //Arrange
        const string source = "@chapter One\n@titlepage\n@title The Manual\n@author A. Writer\n"
            + "@end titlepage\nBody text.\n";

        //Act
        string body = Body(source);

        //Assert
        //A printed manual opens with its title page wherever @titlepage was written.
        body.IndexOf("texinfo-titlepage", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("<h1", StringComparison.Ordinal));
        body.Contains("<p class=\"texinfo-title\">The Manual").Should().BeTrue();
        body.Contains("<p class=\"texinfo-author\">A. Writer").Should().BeTrue();
        //Hoisted, not copied.
        body.Split("texinfo-titlepage").Length.Should().Be(2);
    }

    [Fact]
    public void Generate_drops_menus_and_index_entries_from_the_rendered_document()
    {
        //Arrange
        const string source = "@chapter One\n@menu\n* Start:: The beginning.\n@end menu\n"
            + "@cindex engraving\nText.\n";

        //Act
        string body = Body(source);

        //Assert
        body.Contains("The beginning.").Should().BeFalse();
        body.Contains("engraving").Should().BeFalse();
        body.Contains("Text.").Should().BeTrue();
    }

    // ----- lists and tables ------------------------------------------------------------------

    [Fact]
    public void Generate_renders_an_itemized_list()
    {
        //Arrange + Act
        string body = Body("@itemize @bullet\n@item\nOne\n@item\nTwo\n@end itemize\n");

        //Assert
        body.Contains("<ul>").Should().BeTrue();
        body.Split("<li>").Length.Should().Be(3);
    }

    [Fact]
    public void Generate_carries_an_enumerated_list_starting_value_into_the_markup()
    {
        //Arrange + Act
        string body = Body("@enumerate 3\n@item\nThird\n@end enumerate\n");

        //Assert
        body.Contains("<ol start=\"3\">").Should().BeTrue();
    }

    [Fact]
    public void Generate_maps_a_lettered_enumeration_to_a_list_style()
    {
        //Arrange + Act
        string body = Body("@enumerate a\n@item\nFirst\n@end enumerate\n");

        //Assert
        body.Contains("<ol style=\"list-style-type: lower-alpha\">").Should().BeTrue();
    }

    [Fact]
    public void Generate_renders_a_table_as_a_definition_list_in_its_format_command()
    {
        //Arrange
        const string source = "@table @code\n@item first\nThe first one.\n"
            + "@item second\nThe second one.\n@end table\n";

        //Act
        string body = Body(source);

        //Assert
        body.Contains("<dl class=\"texinfo-table\">").Should().BeTrue();
        body.Contains("<dt><code>first</code>").Should().BeTrue();
        body.Contains("The first one.").Should().BeTrue();
        body.Split("<dd>").Length.Should().Be(3);
    }

    [Fact]
    public void Generate_renders_a_multitable_with_its_column_proportions()
    {
        //Arrange
        const string source = "@multitable @columnfractions .25 .75\n"
            + "@headitem Name @tab Meaning\n@item Alpha @tab The first letter.\n@end multitable\n";

        //Act
        string body = Body(source);

        //Assert
        body.Contains("<table class=\"texinfo-multitable\">").Should().BeTrue();
        body.Contains("<th style=\"width: 25%\">").Should().BeTrue();
        body.Contains("<th style=\"width: 75%\">").Should().BeTrue();
        body.Contains("<td>").Should().BeTrue();
        body.Contains("Alpha").Should().BeTrue();
        //The proportions describe the whole table, so they are written once.
        body.Split("width: 25%").Length.Should().Be(2);
    }

    // ----- directives ------------------------------------------------------------------------

    [Fact]
    public void Generate_renders_a_page_break_directive()
    {
        //Arrange + Act
        string body = Body("One.\n@page\nTwo.\n");

        //Assert
        body.Contains("<div class=\"texinfo-page-break\">").Should().BeTrue();
    }

    [Fact]
    public void Generate_renders_vertical_space_as_blank_lines()
    {
        //Arrange + Act
        string body = Body("One.\n@sp 2\nTwo.\n");

        //Assert
        body.Split("texinfo-blank").Length.Should().Be(3);
    }

    [Fact]
    public void Generate_places_the_copying_text_where_insertcopying_asks_for_it()
    {
        //Arrange
        const string source = "@copying\nPublic domain.\n@end copying\n@chapter One\n"
            + "@insertcopying\n";

        //Act
        string body = Body(source);

        //Assert
        //The block is printed where @insertcopying appears, not where it was written.
        body.IndexOf("Public domain.", StringComparison.Ordinal)
            .Should().BeGreaterThan(body.IndexOf("<h1", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_reports_that_the_index_is_not_rendered_yet()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@chapter One\n@printindex cp\n");

        //Assert
        result.Warnings.Messages.Any(m => m.Contains("@printindex")).Should().BeTrue();
    }

    // ----- inline content --------------------------------------------------------------------

    [Theory]
    [InlineData("@code{x}", "<code>x</code>")]
    [InlineData("@emph{x}", "<em>x</em>")]
    [InlineData("@strong{x}", "<strong>x</strong>")]
    [InlineData("@b{x}", "<b>x</b>")]
    [InlineData("@i{x}", "<i>x</i>")]
    [InlineData("@var{x}", "<i class=\"texinfo-var\">x</i>")]
    [InlineData("@samp{x}", "<samp>x</samp>")]
    [InlineData("@kbd{x}", "<kbd>x</kbd>")]
    [InlineData("@key{x}", "<kbd class=\"texinfo-key\">x</kbd>")]
    [InlineData("@sc{x}", "<span class=\"texinfo-sc\">x</span>")]
    [InlineData("@sup{x}", "<sup>x</sup>")]
    [InlineData("@sub{x}", "<sub>x</sub>")]
    [InlineData("@file{x}", "<code>x</code>")]
    public void Generate_maps_an_inline_command_to_its_element(string source, string expected)
        => Body(source + "\n").Contains(expected).Should().BeTrue();

    [Fact]
    public void Generate_renders_a_glyph_command_as_the_character_it_stands_for()
        => Body("Wait@dots{} done.\n").Contains("Wait… done.").Should().BeTrue();

    [Fact]
    public void Generate_renders_a_url_as_a_link()
    {
        //Arrange + Act
        string body = Body("@uref{https://example.org, Example}\n");

        //Assert
        body.Contains("<a href=\"https://example.org\">Example</a>").Should().BeTrue();
    }

    [Fact]
    public void Generate_renders_an_email_address_as_a_mailto_link()
    {
        //Arrange + Act
        string body = Body("@email{someone@@example.org}\n");

        //Assert
        body.Contains("<a href=\"mailto:someone@example.org\">someone@example.org</a>")
            .Should().BeTrue();
    }

    [Fact]
    public void Generate_renders_a_line_break_command()
        => Body("First@*second\n").Contains("First<br>second").Should().BeTrue();

    [Fact]
    public void Generate_centres_a_centered_line()
        => Body("@center Middle\n").Contains("<p class=\"texinfo-center\">Middle").Should().BeTrue();

    [Fact]
    public void Generate_reports_that_mathematics_was_reduced_to_styled_text()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("The value @math{x^2} grows.\n");

        //Assert
        result.BodyHtml.Contains("<i class=\"texinfo-math\">x^2</i>").Should().BeTrue();
        result.Warnings.Messages.Any(m => m.Contains("@math")).Should().BeTrue();
    }

    [Fact]
    public void Generate_reads_a_cross_reference_correctly_even_though_it_does_not_link_yet()
    {
        //Arrange + Act
        string body = Body("@node Start\n@chapter Start\nMore in @pxref{Start}.\n");

        //Assert
        body.Contains("see Start").Should().BeTrue();
    }

    // ----- footnotes -------------------------------------------------------------------------

    [Fact]
    public void Generate_links_a_footnote_marker_to_its_text_at_the_end_of_the_document()
    {
        //Arrange + Act
        string body = Body("Claim.@footnote{The evidence.}\nMore text.\n");

        //Assert
        body.Contains("<sup class=\"texinfo-footnote-ref\"><a href=\"#footnote-1\">1</a></sup>")
            .Should().BeTrue();
        body.Contains("<div class=\"texinfo-footnotes\">").Should().BeTrue();
        body.Contains("id=\"footnote-1\">(1) The evidence.").Should().BeTrue();
        //The note's text belongs at the end, not where the marker stands.
        body.IndexOf("The evidence.", StringComparison.Ordinal)
            .Should().BeGreaterThan(body.IndexOf("More text.", StringComparison.Ordinal));
    }

    // ----- anchors and images ----------------------------------------------------------------

    [Fact]
    public void Generate_gives_a_standalone_anchor_a_destination_of_its_own()
    {
        //Arrange + Act
        string body = Body("@anchor{Here}\nText.\n");

        //Assert
        body.Contains("<p class=\"texinfo-anchor\" id=\"Here\">").Should().BeTrue();
    }

    [Fact]
    public void Generate_finds_an_image_whose_reference_omits_the_extension()
    {
        //Arrange
        string directory = Directory.CreateTempSubdirectory("texinfo-image-").FullName;
        File.WriteAllBytes(Path.Combine(directory, "diagram.png"), new byte[] { 1, 2, 3 });

        try
        {
            //Act
            string body = new TexinfoHtmlRenderer()
                .Generate("@image{diagram,,,A diagram}\n", directory).BodyHtml;

            //Assert
            body.Contains("<img src=\"" + Path.Combine(directory, "diagram.png") + "\"")
                .Should().BeTrue();
            body.Contains("alt=\"A diagram\"").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Generate_stands_a_missing_image_in_with_its_alternate_text()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@image{absent,,,A diagram}\n");

        //Assert
        //@image is a brace command, so it stands inside the paragraph that holds it.
        result.BodyHtml.Contains("<span class=\"texinfo-missing-image\">[A diagram]</span>")
            .Should().BeTrue();
        result.Warnings.Messages.Any(m => m.Contains("Image 'absent'")).Should().BeTrue();
    }

    [Fact]
    public void Generate_carries_a_usable_image_width_into_the_markup()
    {
        //Arrange
        string directory = Directory.CreateTempSubdirectory("texinfo-image-width-").FullName;
        File.WriteAllBytes(Path.Combine(directory, "wide.png"), new byte[] { 1 });

        try
        {
            //Act
            string body = new TexinfoHtmlRenderer()
                .Generate("@image{wide,4in,,Wide}\n", directory).BodyHtml;

            //Assert
            body.Contains("style=\"width: 4in\"").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ----- music snippets --------------------------------------------------------------------

    [Fact]
    public void Generate_emits_a_music_snippet_as_its_source_and_says_so_once()
    {
        //Arrange
        const string source = "@lilypond[quote]\nc4 d4\n@end lilypond\n"
            + "@lilypond[quote]\ne4 f4\n@end lilypond\n";

        //Act
        TexinfoHtmlResult result = Render(source);

        //Assert
        result.BodyHtml.Contains("<pre class=\"texinfo-lilypond\">").Should().BeTrue();
        result.BodyHtml.Contains("c4 d4").Should().BeTrue();
        result.Warnings.Messages.Count(m => m.Contains("music snippet")).Should().Be(1);
        result.Warnings.Messages.Any(m => m.Contains("2 music snippet")).Should().BeTrue();
    }
}
