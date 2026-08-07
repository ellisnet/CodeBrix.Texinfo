using System.Linq;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Preprocessing;
using CodeBrix.Texinfo2Html.Sources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

public class MacroExpansionTests
{
    private static PreprocessedDocument Process(string source, PreprocessorOptions options = null)
        => new TexinfoPreprocessor(options ?? new PreprocessorOptions())
            .Process(TexinfoSourceText.FromString("test.texi", source), baseDirectory: null);

    private static string Dump(string source, PreprocessorOptions options = null)
        => Process(source, options).DumpExpandedSource();

    // ----- Invocation forms ---------------------------------------------------------------------

    [Fact]
    public void Brace_form_call_expands_inline()
    {
        //Arrange
        string source = "@macro q{TEXT}\n@quoteleft{}\\TEXT\\@quoteright{}\n@end macro\n"
            + "say @q{hi} now\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("say @quoteleft{}hi@quoteright{} now\n");
    }

    [Fact]
    public void Zero_parameter_macro_works_bare_and_with_empty_braces()
    {
        //Arrange
        string source = "@macro smallspace\n@sp 1\n@end macro\n@smallspace\nx @smallspace{} y\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("@sp 1\nx @sp 1 y\n");
    }

    [Fact]
    public void Line_form_call_takes_the_rest_of_the_line_verbatim()
    {
        //Arrange: backslashes in line-form arguments are literal text, the way
        //LilyPond indexes commands such as \relative.
        string source = "@macro funindex{TEXT}\n@findex \\TEXT\\\n@c\n@end macro\n"
            + "@funindex \\once\nafter\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("@findex \\once\nafter\n");
    }

    [Fact]
    public void Line_form_call_keeps_double_backslashes_verbatim()
    {
        //Arrange
        string source = "@macro funindex{TEXT}\n@findex \\TEXT\\\n@c\n@end macro\n"
            + "@funindex \\\\\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("@findex \\\\\n");
    }

    [Fact]
    public void Trailing_comment_in_body_prevents_blank_lines_after_line_form_calls()
    {
        //Arrange: the classic Texinfo idiom - a body ending in @c - must not leave an
        //empty line between two consecutive line-form invocations.
        string source = "@macro fi{T}\n@findex \\T\\\n@c\n@end macro\n"
            + "@fi one\n@fi two\ntext\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("@findex one\n@findex two\ntext\n");
    }

    // ----- Argument splitting -------------------------------------------------------------------

    [Fact]
    public void Multi_parameter_arguments_split_at_top_level_commas()
    {
        //Arrange
        string source = "@macro pair{A,B}\n[\\A\\|\\B\\]\n@end macro\n@pair{one, two}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("[one|two]\n");
    }

    [Fact]
    public void Commas_inside_nested_braces_do_not_split_arguments()
    {
        //Arrange
        string source = "@macro pair{A,B}\n[\\A\\|\\B\\]\n@end macro\n@pair{@code{x, y}, z}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("[@code{x, y}|z]\n");
    }

    [Fact]
    public void Escaped_commas_and_braces_become_literal_characters()
    {
        //Arrange
        string source = "@macro pair{A,B}\n[\\A\\|\\B\\]\n@end macro\n@pair{a\\, b, c\\{d\\}}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("[a, b|c{d}]\n");
    }

    [Fact]
    public void Single_parameter_macro_keeps_commas_in_its_argument()
    {
        //Arrange
        string source = "@macro one{T}\n<\\T\\>\n@end macro\n@one{a, b}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("<a, b>\n");
    }

    [Fact]
    public void Brace_arguments_may_span_multiple_lines()
    {
        //Arrange
        string source = "@macro warn{T}\n@quotation\n\\T\\\n@end quotation\n@end macro\n"
            + "@warn{line one\nline two}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("@quotation\nline one\nline two\n@end quotation\n");
    }

    [Fact]
    public void Missing_trailing_arguments_expand_empty_with_a_warning()
    {
        //Arrange
        string source = "@macro pair{A,B}\n[\\A\\|\\B\\]\n@end macro\n@pair{only}\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("[only|]\n");
        document.Warnings.Count.Should().Be(1);
        document.Warnings[0].Category.Should().Be(TexinfoWarningCategory.Macro);
    }

    [Fact]
    public void Explicit_empty_second_argument_is_not_a_mismatch()
    {
        //Arrange
        string source = "@macro pair{A,B}\n[\\A\\|\\B\\]\n@end macro\n@pair{2_13_13,}\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("[2_13_13|]\n");
        document.Warnings.Count.Should().Be(0);
    }

    // ----- Body substitution --------------------------------------------------------------------

    [Fact]
    public void Double_backslash_in_body_yields_a_literal_backslash()
    {
        //Arrange: LilyPond's @bs macro.
        string source = "@macro bs\n\\\\\n@end macro\nx @bs{}y\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("x \\y\n");
    }

    [Fact]
    public void Parameter_names_with_hyphens_substitute_correctly()
    {
        //Arrange
        string source = "@macro img{IMAGE-FILE, EXT}\n<\\IMAGE-FILE\\.\\EXT\\>\n@end macro\n@img{pic,png}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("<pic.png>\n");
    }

    [Fact]
    public void Unknown_parameter_reference_warns_and_stays_literal()
    {
        //Arrange
        string source = "@macro uu{A}\n\\B\\ and \\A\\\n@end macro\n@uu{x}\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("\\B\\ and x\n");
        document.Warnings.Count.Should().Be(1);
        document.Warnings[0].Message.Contains("\\B\\").Should().BeTrue();
    }

    [Fact]
    public void Argument_text_is_not_rescanned_for_parameters()
    {
        //Arrange: an argument containing \A\ must be inserted verbatim, not substituted again.
        string source = "@macro one{A}\n<\\A\\>\n@end macro\n@one{keep \\A\\ literal}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("<keep \\A\\ literal>\n");
    }

    [Fact]
    public void Macros_may_invoke_other_macros()
    {
        //Arrange
        string source = "@macro inner{T}\n(\\T\\)\n@end macro\n"
            + "@macro outer{T}\n@inner{\\T\\!}\n@end macro\n"
            + "@outer{deep}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("(deep!)\n");
    }

    // ----- Definition table management ----------------------------------------------------------

    [Fact]
    public void Redefinition_is_silent_and_last_definition_wins()
    {
        //Arrange: conditional-branch redefinition is a normal Texinfo idiom, so no warning.
        string source = "@macro m\none\n@end macro\n@macro m\ntwo\n@end macro\n@m{}\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("two\n");
        document.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Unmacro_removes_the_definition()
    {
        //Arrange
        string source = "@macro lydoctitle{T}\n<\\T\\>\n@end macro\n@unmacro lydoctitle\n@lydoctitle Foo\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("@lydoctitle Foo\n");
        document.Macros.ContainsKey("lydoctitle").Should().BeFalse();
    }

    [Fact]
    public void Macro_shadowing_a_builtin_is_rejected_and_the_builtin_survives()
    {
        //Arrange: LilyPond's TeX-branch '@macro cindex' must not break the real @cindex.
        string source = "@macro cindex{T}\nshadow\n@end macro\n@cindex real entry\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("@cindex real entry\n");
        document.Macros.ContainsKey("cindex").Should().BeFalse();
        document.Warnings.Count.Should().Be(1);
        document.Warnings[0].Message.Contains("cindex").Should().BeTrue();
    }

    // ----- Recursion protection -----------------------------------------------------------------

    [Fact]
    public void Non_recursive_macro_calling_itself_is_dropped_with_a_warning()
    {
        //Arrange
        string source = "@macro ma\nx@ma{}\n@end macro\n@ma{}\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("x\n");
        document.Warnings.Count.Should().Be(1);
        document.Warnings[0].Message.Contains("rmacro").Should().BeTrue();
    }

    [Fact]
    public void Runaway_rmacro_recursion_hits_the_depth_cap()
    {
        //Arrange
        PreprocessorOptions options = new PreprocessorOptions { MaxExpansionDepth = 10 };
        string source = "@rmacro loop\n@loop{}\n@end rmacro\n@loop{}\n";

        //Act
        PreprocessedDocument document = Process(source, options);

        //Assert
        document.Warnings.Any(w => w.Message.Contains("deeper than")).Should().BeTrue();
    }

    [Fact]
    public void Mutual_recursion_of_non_recursive_macros_is_stopped()
    {
        //Arrange
        string source = "@macro ma\nA@mb{}\n@end macro\n@macro mb\nB@ma{}\n@end macro\n@ma{}\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("AB\n");
        document.Warnings.Count.Should().Be(1);
    }
}
