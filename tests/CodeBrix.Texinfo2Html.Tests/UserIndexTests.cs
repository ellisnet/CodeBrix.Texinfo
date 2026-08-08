using System;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The rest of the index set: the four predefined indices a general manual uses that a music
/// manual does not, and the indices a document defines for itself with <c>@defindex</c> and
/// <c>@defcodeindex</c>.
/// </summary>
/// <remarks>
/// The distinction the two defining commands draw is which font the printed entries are set in,
/// and that is the only difference between them - so it is what these tests are about.
/// </remarks>
public class UserIndexTests
{
    /// <summary>The fixture: an index of each predefined kind, printed.</summary>
    private const string EveryPredefinedIndex =
        "@settitle Index Fixture\n"
        + "@node Top\n@top Index Fixture\n"
        + "@node One\n@chapter Entries\n"
        + "@cindex a concept\n"
        + "@findex a-function\n"
        + "@vindex a-variable\n"
        + "@kindex C-x C-f\n"
        + "@pindex a-program\n"
        + "@tindex a-type\n"
        + "Text.\n"
        + "@printindex cp\n@printindex fn\n@printindex vr\n"
        + "@printindex ky\n@printindex pg\n@printindex tp\n";

    private static TexinfoHtmlResult Render(string source)
        => new TexinfoHtmlRenderer().Generate(source);

    private static string Body(string source) => Render(source).BodyHtml;

    private static int CountOf(string text, string value) => text.Split(value).Length - 1;

    [Fact]
    public void Every_predefined_index_collects_and_prints_what_its_command_files()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render(EveryPredefinedIndex);

        //Assert - six commands, six indices, six entries, and nothing to report.
        CountOf(result.BodyHtml, "<div class=\"texinfo-index\">").Should().Be(6);
        CountOf(result.BodyHtml, "<p class=\"texinfo-index-entry\">").Should().Be(6);
        result.BodyHtml.Contains("a concept").Should().BeTrue();
        result.BodyHtml.Contains("C-x C-f").Should().BeTrue();
        result.BodyHtml.Contains("a-program").Should().BeTrue();
        string.Join(Environment.NewLine, result.Warnings.Messages).Should().Be(string.Empty);
    }

    [Fact]
    public void The_concept_index_is_the_one_that_is_not_set_in_code()
    {
        //Arrange + Act - the Texinfo manual states the rule that way round, so this checks it that
        //way round too.
        string concept = Body("@cindex a concept\nText.\n@printindex cp\n");
        string function = Body("@findex a-function\nText.\n@printindex fn\n");

        //Assert
        concept.Contains("<code>a concept</code>").Should().BeFalse();
        function.Contains("<code>a-function</code>").Should().BeTrue();
    }

    [Fact]
    public void Defindex_creates_an_index_and_the_command_that_files_into_it()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@defindex fl\n"
            + "@node Top\n@top Doc\n"
            + "@flindex a flag\n"
            + "Text.\n"
            + "@printindex fl\n");

        //Assert - the command did not exist until the document defined it.
        result.BodyHtml.Contains("<div class=\"texinfo-index\">").Should().BeTrue();
        result.BodyHtml.Contains("a flag").Should().BeTrue();
        string.Join(Environment.NewLine, result.Warnings.Messages).Should().Be(string.Empty);
    }

    [Fact]
    public void Defcodeindex_prints_its_entries_in_a_fixed_width_font()
    {
        //Arrange + Act - which font the entries print in is the only difference between the two
        //defining commands.
        string plain = Body("@defindex fl\n@flindex an-entry\nText.\n@printindex fl\n");
        string code = Body("@defcodeindex cd\n@cdindex an-entry\nText.\n@printindex cd\n");

        //Assert
        plain.Contains("<code>an-entry</code>").Should().BeFalse();
        code.Contains("<code>an-entry</code>").Should().BeTrue();
    }

    [Fact]
    public void A_user_index_can_be_merged_into_another_one()
    {
        //Arrange + Act
        string body = Body("@defcodeindex cd\n"
            + "@syncodeindex cd fn\n"
            + "@node Top\n@top Doc\n"
            + "@cdindex from-user-index\n"
            + "@findex from-function-index\n"
            + "Text.\n"
            + "@printindex fn\n");

        //Assert - one printed index holding both, which is what the merge asked for.
        CountOf(body, "<div class=\"texinfo-index\">").Should().Be(1);
        body.Contains("from-user-index").Should().BeTrue();
        body.Contains("from-function-index").Should().BeTrue();
    }

    [Fact]
    public void An_index_command_used_before_its_index_is_defined_is_reported()
    {
        //Arrange + Act - the definition has to come first, and a document that gets it wrong
        //should hear about it rather than lose the entry silently.
        TexinfoHtmlResult result = Render("@flindex too early\n@defindex fl\nText.\n");

        //Assert
        string joined = string.Join("|", result.Warnings.Messages);
        joined.Contains("UnknownCommand:").Should().BeTrue();
    }

    [Fact]
    public void Defindex_with_no_name_is_reported()
    {
        //Arrange + Act
        TexinfoHtmlResult result = Render("@defindex\nText.\n");

        //Assert
        result.Warnings.Messages.Count.Should().Be(1);
        result.Warnings.Messages[0].StartsWith("Syntax:", StringComparison.Ordinal).Should().BeTrue();
    }

    [Fact]
    public void An_ftable_prints_its_terms_in_the_function_index()
    {
        //Arrange + Act - @ftable is @table plus an index entry per term, and this is the half of
        //that claim only a rendered index can show.
        string body = Body("@node Top\n@top Doc\n"
            + "@ftable @code\n@item one\nThe first.\n@item two\nThe second.\n@end ftable\n"
            + "@printindex fn\n");

        //Assert
        CountOf(body, "<p class=\"texinfo-index-entry\">").Should().Be(2);
        body.Contains("<code>one</code>").Should().BeTrue();
        body.Contains("<code>two</code>").Should().BeTrue();
    }

    [Fact]
    public void An_ftable_index_line_links_back_to_the_term_it_came_from()
    {
        //Arrange + Act
        string body = Body("@node Top\n@top Doc\n"
            + "@ftable @code\n@item one\nThe first.\n@end ftable\n"
            + "@printindex fn\n");

        //Assert - the marker sits in the term, and the index line points at it.
        int marker = body.IndexOf("<dt><span id=\"", StringComparison.Ordinal);
        marker.Should().BeGreaterThan(0);
        int start = marker + "<dt><span id=\"".Length;
        string identifier = body.Substring(start, body.IndexOf('"', start) - start);
        body.Contains("href=\"#" + identifier + "\"").Should().BeTrue();
    }
}
