using System;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The commands that shape a printed manual rather than its content: where a chapter starts, what
/// a sectioning command means after the document has shifted it, the short title page, and the one
/// line an author wants standing clear of the indentation around it.
/// </summary>
public class PrintShapeTests
{
    private static TexinfoHtmlResult Render(string source)
        => new TexinfoHtmlRenderer().Generate(source);

    private static string Body(string source) => Render(source).BodyHtml;

    private static int CountOf(string text, string value) => text.Split(value).Length - 1;

    // ----- where a chapter starts ------------------------------------------------------------

    [Fact]
    public void A_chapter_starts_on_a_fresh_page_by_default()
    {
        //Arrange + Act - this is Texinfo's own default and what a printed manual looks like.
        string body = Body("@node Top\n@top Doc\n"
            + "@node A\n@chapter Alpha\nText.\n"
            + "@node B\n@chapter Beta\nMore.\n");

        //Assert - both chapters, and not the @top node above them, which opens no chapter.
        CountOf(body, "class=\"texinfo-chapter\"").Should().Be(2);
    }

    [Fact]
    public void A_section_does_not_start_a_fresh_page()
    {
        //Arrange + Act
        string body = Body("@node Top\n@top Doc\n@node A\n@chapter Alpha\n"
            + "@node B\n@section Inner\nText.\n");

        //Assert
        CountOf(body, "class=\"texinfo-chapter\"").Should().Be(1);
    }

    [Fact]
    public void Setchapternewpage_off_leaves_the_chapters_running_on()
    {
        //Arrange + Act
        string body = Body("@setchapternewpage off\n@node Top\n@top Doc\n"
            + "@node A\n@chapter Alpha\nText.\n"
            + "@node B\n@chapter Beta\nMore.\n");

        //Assert
        body.Contains("texinfo-chapter").Should().BeFalse();
    }

    // ----- @raisesections and @lowersections -------------------------------------------------

    [Fact]
    public void Lowersections_makes_the_next_chapter_a_section_of_the_one_before_it()
    {
        //Arrange + Act - this is how a manual folds an included file in one level deeper than it
        //was written.
        TexinfoHtmlResult result = Render("@node Top\n@top Doc\n"
            + "@node A\n@chapter Alpha\nText.\n"
            + "@lowersections\n"
            + "@node B\n@chapter Beta\nMore.\n");

        //Assert - Beta now hangs under Alpha, so it is numbered as its first subdivision.
        result.BodyHtml.Contains("1.1").Should().BeTrue();
        result.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Raisesections_lifts_a_section_up_to_chapter_level()
    {
        //Arrange + Act
        string body = Body("@node Top\n@top Doc\n"
            + "@node A\n@chapter Alpha\nText.\n"
            + "@raisesections\n"
            + "@node B\n@section Beta\nMore.\n");

        //Assert - Beta is a chapter now, so it takes the next chapter number rather than 1.1, and
        //it starts a fresh page as any other chapter does.
        body.Contains(">2<").Should().BeTrue();
        CountOf(body, "class=\"texinfo-chapter\"").Should().Be(2);
    }

    [Fact]
    public void The_two_shifts_cancel_each_other_out()
    {
        //Arrange + Act
        string body = Body("@node Top\n@top Doc\n"
            + "@lowersections\n@raisesections\n"
            + "@node A\n@chapter Alpha\nText.\n"
            + "@node B\n@chapter Beta\nMore.\n");

        //Assert - two chapters, numbered 1 and 2 as if nothing had happened.
        CountOf(body, "class=\"texinfo-chapter\"").Should().Be(2);
        body.Contains(">2<").Should().BeTrue();
    }

    // ----- @shorttitlepage -------------------------------------------------------------------

    [Fact]
    public void A_short_title_page_is_a_title_page_carrying_only_the_title()
    {
        //Arrange + Act
        string body = Body("@shorttitlepage The Pocket Manual\n"
            + "@node Top\n@top The Pocket Manual\nText.\n");

        //Assert - it is styled and placed exactly as the @titlepage form is.
        body.Contains("<div class=\"texinfo-titlepage\">").Should().BeTrue();
        body.Contains("<p class=\"texinfo-title\">The Pocket Manual").Should().BeTrue();
    }

    [Fact]
    public void A_short_title_page_is_hoisted_to_the_front_of_the_document()
    {
        //Arrange + Act - a printed manual opens with its title page wherever the command was
        //written, which for this one is after a chapter has already started.
        string body = Body("@node Top\n@top Doc\n@shorttitlepage The Pocket Manual\nText.\n");

        //Assert
        body.IndexOf("texinfo-titlepage", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("<h1", StringComparison.Ordinal));
    }

    // ----- @exdent ---------------------------------------------------------------------------

    [Fact]
    public void Exdent_moves_its_line_out_of_the_indentation_around_it()
    {
        //Arrange + Act
        string body = Body("@quotation\nIndented text.\n@exdent Out to the margin.\n@end quotation\n");

        //Assert
        body.Contains("<p class=\"texinfo-exdent\">Out to the margin.").Should().BeTrue();
        body.Contains("Indented text.").Should().BeTrue();
    }

    [Fact]
    public void Exdent_renders_without_a_warning()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@exdent A line of its own.\n");

        //Assert
        result.Warnings.Count.Should().Be(0);
        result.BodyHtml.Contains("A line of its own.").Should().BeTrue();
    }
}
