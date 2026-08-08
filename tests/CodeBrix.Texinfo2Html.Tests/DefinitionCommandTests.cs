using System;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The definition commands - <c>@deffn</c> and the twenty-one relatives that describe functions,
/// variables, data types and the members of a class. Every fixture here is original Texinfo
/// written for the test.
/// </summary>
/// <remarks>
/// Two things are being checked throughout: that the heading line is taken apart correctly - a
/// braced group is one word however many spaces it holds, and the parts land in the order each
/// command declares - and that the name reaches the index the Texinfo manual names for it.
/// </remarks>
public class DefinitionCommandTests
{
    /// <summary>
    /// The fixture: one definition of each shape the family has - read category and fixed
    /// category, with and without a data type, and with and without a class.
    /// </summary>
    private const string EveryShape =
        "@settitle Definition Shapes\n"
        + "@node Top\n@top Definition Shapes\n"
        + "@node One\n@chapter Entities\n"
        + "@deffn {Interactive Command} isearch-forward count\n"
        + "Searches forward.\n"
        + "@end deffn\n"
        + "@defun make-list length object\n"
        + "Builds a list.\n"
        + "@end defun\n"
        + "@deftypefn {Library Function} int foobar (int @var{foo}, float @var{bar})\n"
        + "Does something typed.\n"
        + "@end deftypefn\n"
        + "@defvr {User Option} fill-column\n"
        + "The column filling stops at.\n"
        + "@end defvr\n"
        + "@deftypevar int frame-count\n"
        + "How many frames there are.\n"
        + "@end deftypevar\n"
        + "@deftp {Data type} pair car cdr\n"
        + "A pair.\n"
        + "@end deftp\n"
        + "@defcv {Class Option} Window border-pattern\n"
        + "A class option.\n"
        + "@end defcv\n"
        + "@defmethod Window expose sides\n"
        + "Exposes the window.\n"
        + "@end defmethod\n"
        + "@printindex fn\n"
        + "@printindex vr\n"
        + "@printindex tp\n";

    private static string Body(string source) => new TexinfoHtmlRenderer().Generate(source).BodyHtml;

    private static int CountOf(string text, string value) => text.Split(value).Length - 1;

    // ----- the heading line ------------------------------------------------------------------

    [Fact]
    public void A_definition_is_a_description_list_of_heading_lines_over_one_description()
    {
        //Arrange + Act
        string body = Body("@deffn Command forward-word count\nMoves point.\n@end deffn\n");

        //Assert
        body.Contains("<dl class=\"texinfo-definition\">").Should().BeTrue();
        body.Contains("<dt class=\"texinfo-def-line\">").Should().BeTrue();
        body.Contains("<span class=\"texinfo-def-category\">Command:</span>").Should().BeTrue();
        body.Contains("<code class=\"texinfo-def-name\">forward-word</code>").Should().BeTrue();
        body.Contains("<i class=\"texinfo-def-arg\">count</i>").Should().BeTrue();
        body.Contains("<dd>").Should().BeTrue();
        body.Contains("Moves point.").Should().BeTrue();
    }

    [Fact]
    public void A_braced_category_is_one_word_however_many_spaces_it_holds()
    {
        //Arrange + Act - without the braces, 'Command' would be the category and 'Interactive'
        //would be mistaken for the name, which is the trap the Texinfo manual warns about.
        string body = Body("@deffn {Interactive Command} isearch-forward\nSearches.\n@end deffn\n");

        //Assert
        body.Contains("<span class=\"texinfo-def-category\">Interactive Command:</span>")
            .Should().BeTrue();
        body.Contains("<code class=\"texinfo-def-name\">isearch-forward</code>").Should().BeTrue();
    }

    [Fact]
    public void A_specialized_command_supplies_the_category_the_line_does_not()
    {
        //Arrange + Act
        string body = Body("@defun make-list length\nBuilds a list.\n@end defun\n");

        //Assert - @defun is @deffn Function, so 'make-list' is the name and not the category.
        body.Contains("<span class=\"texinfo-def-category\">Function:</span>").Should().BeTrue();
        body.Contains("<code class=\"texinfo-def-name\">make-list</code>").Should().BeTrue();
        body.Contains("<i class=\"texinfo-def-arg\">length</i>").Should().BeTrue();
    }

    [Fact]
    public void A_typed_definition_puts_the_data_type_before_the_name()
    {
        //Arrange + Act
        string body = Body("@deftypefn {Library Function} int foobar (int @var{foo})\n"
            + "Typed.\n@end deftypefn\n");

        //Assert
        body.Contains("<span class=\"texinfo-def-category\">Library Function:</span>")
            .Should().BeTrue();
        body.Contains("<code class=\"texinfo-def-type\">int</code>").Should().BeTrue();
        body.Contains("<code class=\"texinfo-def-name\">foobar</code>").Should().BeTrue();
        //A typed line is computer text throughout, so its arguments are set in code rather than
        //as the metasyntactic variables an untyped definition's arguments stand for.
        body.Contains("<code class=\"texinfo-def-arg\">").Should().BeTrue();
        body.Contains("<i class=\"texinfo-var\">foo</i>").Should().BeTrue();
    }

    [Fact]
    public void A_class_member_names_the_class_the_way_its_command_says()
    {
        //Arrange + Act - a variable is 'of' its class and a method is 'on' it.
        string variable = Body("@defcv {Class Option} Window border\nAn option.\n@end defcv\n");
        string method = Body("@defmethod Window expose sides\nExposes it.\n@end defmethod\n");

        //Assert
        variable.Contains("<span class=\"texinfo-def-category\">Class Option of Window:</span>")
            .Should().BeTrue();
        method.Contains("<span class=\"texinfo-def-category\">Method on Window:</span>")
            .Should().BeTrue();
    }

    [Fact]
    public void A_heading_line_continues_over_a_lone_at_sign_at_the_end_of_a_line()
    {
        //Arrange + Act - the one context in Texinfo where '@' continues a line.
        string body = Body("@deftypefn {Library Function} int foobar @\n"
            + "  (int @var{foo}, float @var{bar})\n"
            + "Continued.\n@end deftypefn\n");

        //Assert - the continuation is a word separator, so the name is still just 'foobar'.
        body.Contains("<code class=\"texinfo-def-name\">foobar</code>").Should().BeTrue();
        body.Contains("float").Should().BeTrue();
    }

    [Fact]
    public void An_x_form_adds_a_second_heading_line_to_the_same_description()
    {
        //Arrange + Act
        string body = Body("@deffn {Interactive Command} isearch-forward\n"
            + "@deffnx {Interactive Command} isearch-backward\n"
            + "These two are similar.\n"
            + "@end deffn\n");

        //Assert - one list, two terms, one description.
        CountOf(body, "<dl class=\"texinfo-definition\">").Should().Be(1);
        CountOf(body, "<dt class=\"texinfo-def-line\">").Should().Be(2);
        CountOf(body, "<dd>").Should().Be(1);
        body.Contains("isearch-backward").Should().BeTrue();
    }

    [Fact]
    public void The_arguments_of_an_untyped_definition_keep_their_punctuation()
    {
        //Arrange + Act - the bracket-and-ellipsis convention for optional and repeated arguments.
        string body = Body("@defspec foobar (var [from to [inc]]) body@dots{}\n"
            + "A complicated form.\n@end defspec\n");

        //Assert
        body.Contains("<span class=\"texinfo-def-category\">Special Form:</span>").Should().BeTrue();
        body.Contains("(var [from to [inc]]) body…").Should().BeTrue();
    }

    // ----- the index entries -----------------------------------------------------------------

    [Fact]
    public void Every_definition_files_its_name_in_the_index_its_command_names()
    {
        //Arrange + Act
        string body = Body(EveryShape);

        //Assert - three printed indices, each holding the names its family contributed.
        CountOf(body, "<div class=\"texinfo-index\">").Should().Be(3);
        body.Contains(">isearch-forward<").Should().BeTrue();
        body.Contains(">make-list<").Should().BeTrue();
        body.Contains(">foobar<").Should().BeTrue();
        body.Contains(">fill-column<").Should().BeTrue();
        body.Contains(">frame-count<").Should().BeTrue();
        body.Contains(">pair<").Should().BeTrue();
    }

    [Fact]
    public void A_class_members_index_entry_names_its_class_too()
    {
        //Arrange + Act - two classes routinely define a member of the same name, so an index of
        //bare names could not tell them apart.
        string body = Body("@node Top\n@top Members\n"
            + "@defcv {Class Option} Window border\nAn option.\n@end defcv\n"
            + "@defmethod Window expose sides\nExposes it.\n@end defmethod\n"
            + "@printindex fn\n@printindex vr\n");

        //Assert
        body.Contains("border of Window").Should().BeTrue();
        body.Contains("expose on Window").Should().BeTrue();
    }

    [Fact]
    public void A_printed_index_entry_links_back_to_the_definition_that_filed_it()
    {
        //Arrange + Act
        string body = Body("@node Top\n@top Entities\n"
            + "@defun make-list length\nBuilds a list.\n@end defun\n"
            + "@printindex fn\n");

        //Assert - the marker sits in the heading line, and the index line points at it.
        int marker = body.IndexOf("<dt class=\"texinfo-def-line\"><span id=\"", StringComparison.Ordinal);
        marker.Should().BeGreaterThan(0);
        int start = body.IndexOf("id=\"", marker, StringComparison.Ordinal) + 4;
        string identifier = body.Substring(start, body.IndexOf('"', start) - start);
        body.Contains("href=\"#" + identifier + "\"").Should().BeTrue();
    }

    [Fact]
    public void A_definition_that_prints_no_index_leaves_no_marker_behind()
    {
        //Arrange + Act - a marker nothing points at is clutter, and a manual with a thousand
        //definitions and no index would otherwise carry a thousand of them.
        string body = Body("@defun make-list length\nBuilds a list.\n@end defun\n");

        //Assert
        body.Contains("<dt class=\"texinfo-def-line\"><span id=").Should().BeFalse();
    }

    // ----- @defblock -------------------------------------------------------------------------

    [Fact]
    public void A_defblock_groups_definition_lines_that_file_no_index_entries()
    {
        //Arrange + Act
        string body = Body("@node Top\n@top Generic\n"
            + "@defblock\n"
            + "@defline Macro mac (arg1, arg2)\n"
            + "Description of mac.\n"
            + "@deftypeline Builtin int foo (int @var{bar})\n"
            + "Description of foo.\n"
            + "@end defblock\n"
            + "@printindex fn\n");

        //Assert - two definitions inside one block, each with its own description...
        body.Contains("<div class=\"texinfo-defblock\">").Should().BeTrue();
        CountOf(body, "<dl class=\"texinfo-definition\">").Should().Be(2);
        body.Contains("Description of mac.").Should().BeTrue();
        body.Contains("Description of foo.").Should().BeTrue();
        //...and nothing in the index, which is the whole point of these two commands.
        body.Contains("<div class=\"texinfo-index\">").Should().BeFalse();
    }

    [Fact]
    public void Consecutive_definition_lines_share_one_description()
    {
        //Arrange + Act
        string body = Body("@defblock\n"
            + "@defline Function set-var (value)\n"
            + "@defline {Settable Variable} var\n"
            + "Description of set-var and var.\n"
            + "@end defblock\n");

        //Assert
        CountOf(body, "<dl class=\"texinfo-definition\">").Should().Be(1);
        CountOf(body, "<dt class=\"texinfo-def-line\">").Should().Be(2);
        CountOf(body, "<dd>").Should().Be(1);
    }

    [Fact]
    public void A_definition_command_built_from_a_linemacro_renders_as_a_real_definition()
    {
        //Arrange: defining a definition command is what @linemacro exists for, so the two
        //features are only worth anything together - this is that seam.
        //Act
        string body = Body("@linemacro defbuiltin{name, args}\n"
            + "@defline {Builtin} \\name\\ \\args\\\n"
            + "@end linemacro\n"
            + "@defblock\n"
            + "@defbuiltin foo (bar)\n"
            + "Explanation.\n"
            + "@end defblock\n");

        //Assert
        CountOf(body, "<dl class=\"texinfo-definition\">").Should().Be(1);
        body.Contains("Builtin").Should().BeTrue();
        body.Contains("foo").Should().BeTrue();
        body.Contains("(bar)").Should().BeTrue();
        body.Contains("Explanation.").Should().BeTrue();
    }

    // ----- degradations ----------------------------------------------------------------------

    [Fact]
    public void A_definition_with_no_name_is_reported_and_still_rendered()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer().Generate("@deffn\nEmpty.\n@end deffn\n");

        //Assert
        result.Warnings.Messages.Count.Should().Be(1);
        result.Warnings.Messages[0].StartsWith("Syntax:", StringComparison.Ordinal).Should().BeTrue();
        result.BodyHtml.Contains("Empty.").Should().BeTrue();
    }

    [Fact]
    public void An_x_form_with_nothing_to_continue_is_reported_and_still_rendered()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer()
            .Generate("@deffnx Command orphan arg\nText.\n");

        //Assert
        string joined = string.Join("|", result.Warnings.Messages);
        joined.Contains("continues a definition that was never opened").Should().BeTrue();
        result.BodyHtml.Contains("orphan").Should().BeTrue();
    }

    [Fact]
    public void The_flag_that_omits_the_space_after_a_name_omits_it_before_a_bracket_only()
    {
        //Arrange + Act
        string bracketed = Body("@set txidefnamenospace\n"
            + "@deffn Builtin index (string, substring)\nText.\n@end deffn\n");
        string plain = Body("@set txidefnamenospace\n"
            + "@deffn Command forward-word count\nText.\n@end deffn\n");

        //Assert - the flag exists for a language whose syntax writes no space before the bracket;
        //an ordinary argument list still reads as words and keeps its space.
        bracketed.Contains("</code><i class=\"texinfo-def-arg\">(string, substring)</i>")
            .Should().BeTrue();
        plain.Contains("</code> <i class=\"texinfo-def-arg\">count</i>").Should().BeTrue();
    }
}
