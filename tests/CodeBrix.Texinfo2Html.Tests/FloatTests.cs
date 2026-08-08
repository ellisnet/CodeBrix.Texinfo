using System;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// <c>@float</c> and its companions. A float is numbered within its chapter and within its own
/// type, carries a caption, and is what a cross reference means when it says "see Figure 1.2" -
/// so the numbering, the caption and the wording of a reference are one feature and are tested
/// together.
/// </summary>
public class FloatTests
{
    /// <summary>
    /// The fixture: two types of float across two chapters, one of them with a short caption, and
    /// references to floats in both chapters.
    /// </summary>
    private const string TwoChapters =
        "@settitle Float Fixture\n"
        + "@node Top\n@top Float Fixture\n"
        + "@node One\n@chapter First\n"
        + "@float Figure,fig:alpha\n"
        + "A picture would go here.\n"
        + "@caption{The alpha figure}\n"
        + "@end float\n"
        + "@float Figure,fig:beta\n"
        + "Another one.\n"
        + "@caption{The beta figure}\n"
        + "@shortcaption{Beta}\n"
        + "@end float\n"
        + "@float Table,tab:one\n"
        + "A table.\n"
        + "@caption{A table caption}\n"
        + "@end float\n"
        + "@node Two\n@chapter Second\n"
        + "@float Figure,fig:gamma\n"
        + "Third picture.\n"
        + "@caption{The gamma figure}\n"
        + "@end float\n"
        + "See @ref{fig:alpha} and @ref{fig:gamma}.\n"
        + "@listoffloats Figure\n";

    private static string Body(string source) => new TexinfoHtmlRenderer().Generate(source).BodyHtml;

    private static int CountOf(string text, string value) => text.Split(value).Length - 1;

    [Fact]
    public void A_float_is_a_container_with_its_caption_printed_under_it()
    {
        //Arrange + Act
        string body = Body("@float Figure,fig:one\nContent.\n@caption{A caption}\n@end float\n");

        //Assert
        body.Contains("<div class=\"texinfo-float\"").Should().BeTrue();
        body.Contains("<p class=\"texinfo-float-caption\">").Should().BeTrue();
        //The content comes before the caption, which is what "under it" means on the page.
        body.IndexOf("Content.", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("A caption", StringComparison.Ordinal));
    }

    [Fact]
    public void Floats_are_numbered_within_their_chapter_and_within_their_type()
    {
        //Arrange + Act
        string body = Body(TwoChapters);

        //Assert - figures and tables count separately, and both start again in the next chapter.
        body.Contains("Figure 1.1: The alpha figure").Should().BeTrue();
        body.Contains("Figure 1.2: The beta figure").Should().BeTrue();
        body.Contains("Table 1.1: A table caption").Should().BeTrue();
        body.Contains("Figure 2.1: The gamma figure").Should().BeTrue();
    }

    [Fact]
    public void A_reference_to_a_float_reads_as_its_type_and_number()
    {
        //Arrange + Act
        string body = Body(TwoChapters);

        //Assert - the label 'fig:alpha' would tell a reader nothing, so the number is what the
        //reference says instead.
        body.Contains(">Figure 1.1</a> and ").Should().BeTrue();
        body.Contains(">Figure 2.1</a>").Should().BeTrue();
        body.Contains("fig:alpha<").Should().BeFalse();
    }

    [Fact]
    public void A_float_in_an_unnumbered_chapter_counts_through_the_document()
    {
        //Arrange + Act - there is no chapter number to build "1.1" on, so a plain count is all
        //that is left, and it still tells two floats apart.
        string body = Body("@node Top\n@top Doc\n"
            + "@node One\n@unnumbered First\n"
            + "@float Figure,f1\nOne.\n@caption{First}\n@end float\n"
            + "@float Figure,f2\nTwo.\n@caption{Second}\n@end float\n");

        //Assert
        body.Contains("Figure 1: First").Should().BeTrue();
        body.Contains("Figure 2: Second").Should().BeTrue();
    }

    [Fact]
    public void A_list_of_floats_links_every_float_of_the_named_type()
    {
        //Arrange + Act
        string body = Body(TwoChapters);

        //Assert - three figures listed, and the table left out because it is not one.
        body.Contains("<div class=\"texinfo-listoffloats\">").Should().BeTrue();
        CountOf(body, "<p class=\"texinfo-listoffloats-entry\">").Should().Be(3);
        body.Contains("A table caption</a>").Should().BeFalse();
    }

    [Fact]
    public void A_list_of_floats_prefers_the_short_caption_when_there_is_one()
    {
        //Arrange + Act
        string body = Body(TwoChapters);
        int listStart = body.IndexOf("texinfo-listoffloats", StringComparison.Ordinal);
        string list = body.Substring(listStart);

        //Assert - a short caption exists precisely so a list can carry something briefer.
        list.Contains("Figure 1.2</a>: Beta").Should().BeTrue();
        list.Contains("Figure 1.1</a>: The alpha figure").Should().BeTrue();
    }

    [Fact]
    public void A_list_of_floats_of_a_type_the_document_has_none_of_is_reported()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer()
            .Generate("@float Figure,f1\nOne.\n@end float\n@listoffloats Listing\n");

        //Assert
        string joined = string.Join("|", result.Warnings.Messages);
        joined.Contains("Emit:").Should().BeTrue();
        joined.Contains("the document has no float of that type").Should().BeTrue();
    }

    [Fact]
    public void A_float_with_no_label_still_numbers_and_captions()
    {
        //Arrange + Act - only a cross reference needs the label, and not every float has one.
        string body = Body("@node Top\n@top Doc\n@node One\n@chapter First\n"
            + "@float Figure\nContent.\n@caption{No label here}\n@end float\n");

        //Assert
        body.Contains("Figure 1.1: No label here").Should().BeTrue();
    }

    [Fact]
    public void A_caption_written_outside_a_float_is_reported_and_kept()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer().Generate("@caption{Stray text}\n");

        //Assert
        string joined = string.Join("|", result.Warnings.Messages);
        joined.Contains("appears outside a '@float'").Should().BeTrue();
        result.BodyHtml.Contains("Stray text").Should().BeTrue();
    }

    [Fact]
    public void A_float_renders_without_a_warning()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer().Generate(TwoChapters);

        //Assert
        string.Join(Environment.NewLine, result.Warnings.Messages).Should().Be(string.Empty);
    }
}
