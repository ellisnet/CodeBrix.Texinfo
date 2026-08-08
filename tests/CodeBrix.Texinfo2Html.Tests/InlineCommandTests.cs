using System;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The inline commands a general Texinfo manual reaches for that a music manual never does:
/// <c>@verb</c>, <c>@acronym</c>, <c>@abbr</c> and the <c>@inline...</c> conditionals.
/// </summary>
public class InlineCommandTests
{
    /// <summary>The fixture: one of each, written the way the Texinfo manual writes them.</summary>
    private const string EveryInline =
        "Verb: @verb{|literal @code{x} --- text|} done.\n"
        + "Acronym: @acronym{NASA} and @acronym{NASA, National Aeronautics and Space Administration}.\n"
        + "Abbreviation: @abbr{Comput.} and @abbr{Comput., Computer}.\n"
        + "Conditional: @inlinefmt{tex, printed} @inlinefmt{html, browsed}\n"
        + "@inlinefmtifelse{tex, chose-tex, chose-other}\n";

    private static TexinfoHtmlResult Render(string source)
        => new TexinfoHtmlRenderer().Generate(source);

    private static string Body(string source) => Render(source).BodyHtml;

    // ----- @verb -----------------------------------------------------------------------------

    [Fact]
    public void Verb_keeps_every_character_between_the_delimiters_it_was_given()
    {
        //Arrange + Act - the character after the brace is the delimiter, so everything up to the
        //next one is text however much of it looks like Texinfo.
        string body = Body("@verb{|literal @code{x} --- text|}\n");

        //Assert
        body.Contains("<code>literal @code{x} --- text</code>").Should().BeTrue();
        //A run of hyphens inside @verb is not an em dash: that is the whole point of the command.
        body.Contains("—").Should().BeFalse();
    }

    [Theory]
    //Any character may serve as the delimiter, which is what lets @verb quote any text at all.
    [InlineData("@verb{|a|b|}", "a|b")]
    [InlineData("@verb{+a|b+}", "a|b")]
    [InlineData("@verb{!braces {and} more!}", "braces {and} more")]
    public void Verb_takes_its_delimiter_from_the_character_after_the_brace(string source,
        string expected)
        => Body(source + "\n").Contains("<code>" + expected + "</code>").Should().BeTrue();

    [Fact]
    public void Verb_that_is_never_closed_is_reported()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@verb{|never closed\n");

        //Assert
        string joined = string.Join("|", result.Warnings.Messages);
        joined.Contains("Syntax:").Should().BeTrue();
        joined.Contains("is missing its closing").Should().BeTrue();
    }

    // ----- @acronym and @abbr ----------------------------------------------------------------

    [Fact]
    public void An_acronym_is_set_in_small_capitals()
    {
        //Arrange + Act
        string body = Body("@acronym{NASA}\n");

        //Assert
        body.Contains("<span class=\"texinfo-sc\">NASA</span>").Should().BeTrue();
    }

    [Fact]
    public void An_acronym_prints_the_words_it_stands_for_in_parentheses()
    {
        //Arrange + Act
        string body = Body("@acronym{NASA, National Aeronautics and Space Administration}\n");

        //Assert
        body.Contains("<span class=\"texinfo-sc\">NASA</span> (National Aeronautics and Space "
            + "Administration)").Should().BeTrue();
    }

    [Fact]
    public void An_abbreviation_reads_as_written_and_still_takes_a_meaning()
    {
        //Arrange + Act - @abbr differs from @acronym only in not being set in small capitals.
        string body = Body("@abbr{Comput., Computer}\n");

        //Assert
        body.Contains("Comput. (Computer)").Should().BeTrue();
        body.Contains("texinfo-sc").Should().BeFalse();
    }

    // ----- the inline conditionals -----------------------------------------------------------

    [Fact]
    public void Inlinefmt_keeps_the_branch_the_output_profile_asks_for()
    {
        //Arrange + Act - the print profile reads the TeX branch, which is the same rule @iftex
        //follows and the reason a PDF is what this library produces.
        string body = Body("@inlinefmt{tex, printed} @inlinefmt{html, browsed}\n");

        //Assert
        body.Contains("printed").Should().BeTrue();
        body.Contains("browsed").Should().BeFalse();
    }

    [Fact]
    public void Inlinefmt_follows_the_profile_when_it_is_changed()
    {
        //Arrange
        TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
        renderer.Options.ConditionalProfile = TexinfoConditionalProfile.Html;

        //Act
        string body = renderer.Generate("@inlinefmt{tex, printed} @inlinefmt{html, browsed}\n")
            .BodyHtml;

        //Assert
        body.Contains("browsed").Should().BeTrue();
        body.Contains("printed").Should().BeFalse();
    }

    [Fact]
    public void Inlinefmtifelse_takes_the_other_branch_when_the_format_does_not_match()
    {
        //Arrange + Act
        string chosen = Body("@inlinefmtifelse{tex, chose-tex, chose-other}\n");
        string other = Body("@inlinefmtifelse{html, chose-html, chose-other}\n");

        //Assert
        chosen.Contains("chose-tex").Should().BeTrue();
        other.Contains("chose-other").Should().BeTrue();
        other.Contains("chose-html").Should().BeFalse();
    }

    [Fact]
    public void The_chosen_branch_is_still_Texinfo_and_is_processed_as_such()
    {
        //Arrange + Act - the branch is put back into the source rather than copied to the output,
        //so a command inside it means what it always means.
        string body = Body("@inlinefmt{tex, @code{value} and @'e}\n");

        //Assert
        body.Contains("<code>value</code>").Should().BeTrue();
        body.Contains("é").Should().BeTrue();
    }

    [Fact]
    public void Inlineraw_is_skipped_for_the_same_reason_a_raw_block_is()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("Before @inlineraw{tex, \\hbox{raw}} after.\n");

        //Assert
        result.Warnings.Messages.Count.Should().Be(1);
        result.Warnings.Messages[0].StartsWith("RawBlockSkipped:", StringComparison.Ordinal)
            .Should().BeTrue();
        result.BodyHtml.Contains("hbox").Should().BeFalse();
        result.BodyHtml.Contains("Before").Should().BeTrue();
        result.BodyHtml.Contains("after.").Should().BeTrue();
    }

    [Fact]
    public void Inlineifset_and_inlineifclear_read_the_flags_as_they_stood()
    {
        //Arrange + Act
        string body = Body("@set aflag\n"
            + "@inlineifset{aflag, was-set} @inlineifclear{aflag, was-clear}\n"
            + "@inlineifset{bflag, b-set} @inlineifclear{bflag, b-clear}\n");

        //Assert
        body.Contains("was-set").Should().BeTrue();
        body.Contains("was-clear").Should().BeFalse();
        body.Contains("b-set").Should().BeFalse();
        body.Contains("b-clear").Should().BeTrue();
    }

    [Fact]
    public void An_inline_conditional_naming_an_unknown_format_is_reported()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@inlinefmt{nosuchformat, text}\n");

        //Assert
        string joined = string.Join("|", result.Warnings.Messages);
        joined.Contains("Conditional:").Should().BeTrue();
        joined.Contains("unknown output format").Should().BeTrue();
    }

    [Fact]
    public void Every_inline_command_the_fixture_writes_renders_without_a_warning()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render(EveryInline);

        //Assert
        string.Join(Environment.NewLine, result.Warnings.Messages).Should().Be(string.Empty);
        result.BodyHtml.Contains("literal @code{x} --- text").Should().BeTrue();
        result.BodyHtml.Contains("(Computer)").Should().BeTrue();
        result.BodyHtml.Contains("chose-tex").Should().BeTrue();
    }
}
