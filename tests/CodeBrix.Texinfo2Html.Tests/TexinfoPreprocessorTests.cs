using System;
using System.IO;
using System.Linq;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Preprocessing;
using CodeBrix.Texinfo2Html.Sources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

public class TexinfoPreprocessorTests
{
    private static PreprocessedDocument Process(string source, PreprocessorOptions options = null)
        => new TexinfoPreprocessor(options ?? new PreprocessorOptions())
            .Process(TexinfoSourceText.FromString("test.texi", source), baseDirectory: null);

    private static string Dump(string source, PreprocessorOptions options = null)
        => Process(source, options).DumpExpandedSource();

    // ----- @set / @clear / @value ------------------------------------------------------------

    [Fact]
    public void Process_set_and_value_substitute_the_flag_text()
    {
        //Arrange
        string source = "@set version 2.25\nLilyPond @value{version} here\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("LilyPond 2.25 here\n");
    }

    [Fact]
    public void Process_value_of_flag_set_without_text_expands_to_nothing()
    {
        //Arrange
        string source = "@set FDL\nx@value{FDL}y\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("xy\n");
    }

    [Fact]
    public void Process_value_with_commands_is_reprocessed()
    {
        //Arrange
        string source = "@set intro See @code{this}\n@value{intro}!\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("See @code{this}!\n");
    }

    [Fact]
    public void Process_undefined_value_warns_and_leaves_a_marker()
    {
        //Arrange
        string source = "x @value{nope} y\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("x {No value for 'nope'} y\n");
        document.Warnings.Count.Should().Be(1);
        document.Warnings[0].Category.Should().Be(TexinfoWarningCategory.Value);
    }

    [Fact]
    public void Process_cleared_flag_no_longer_satisfies_ifset()
    {
        //Arrange
        string source = "@set web\n@clear web\n@ifset web\nweb-on\n@end ifset\nrest\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("rest\n");
    }

    [Fact]
    public void Process_predefined_values_behave_like_leading_set_lines()
    {
        //Arrange
        PreprocessorOptions options = new PreprocessorOptions();
        options.PredefinedValues["bigpage"] = string.Empty;
        string source = "@ifset bigpage\nbig\n@end ifset\n";

        //Act
        string dump = Dump(source, options);

        //Assert
        dump.Should().Be("big\n");
    }

    [Fact]
    public void Process_circular_value_references_terminate_with_a_warning()
    {
        //Arrange
        string source = "@set a @value{b}\n@set b @value{a}\n@value{a}\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.Warnings.Any(w => w.Category == TexinfoWarningCategory.Macro).Should().BeTrue();
    }

    // ----- Conditional profiles ---------------------------------------------------------------

    [Fact]
    public void Process_print_profile_takes_tex_nottex_nothtml_and_notinfo_branches()
    {
        //Arrange
        string source =
            "@iftex\ntex-on\n@end iftex\n"
            + "@ifnottex\nnottex-on\n@end ifnottex\n"
            + "@ifhtml\nhtml-off\n@end ifhtml\n"
            + "@ifnothtml\nnothtml-on\n@end ifnothtml\n"
            + "@ifinfo\ninfo-off\n@end ifinfo\n"
            + "@ifnotinfo\nnotinfo-on\n@end ifnotinfo\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("tex-on\nnottex-on\nnothtml-on\nnotinfo-on\n");
    }

    [Fact]
    public void Process_html_profile_takes_html_and_skips_nothtml_and_tex()
    {
        //Arrange
        PreprocessorOptions options = new PreprocessorOptions { Profile = ConditionalProfile.Html };
        string source =
            "@iftex\ntex-off\n@end iftex\n"
            + "@ifnottex\nnottex-on\n@end ifnottex\n"
            + "@ifhtml\nhtml-on\n@end ifhtml\n"
            + "@ifnothtml\nnothtml-off\n@end ifnothtml\n";

        //Act
        string dump = Dump(source, options);

        //Assert
        dump.Should().Be("nottex-on\nhtml-on\n");
    }

    [Fact]
    public void Process_nested_same_conditional_is_skipped_as_one_region()
    {
        //Arrange
        string source = "@ifhtml\n@ifhtml\ninner\n@end ifhtml\nouter-rest\n@end ifhtml\nkept\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("kept\n");
        document.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Process_skipped_region_defines_nothing_and_includes_nothing()
    {
        //Arrange
        string source = "@ifhtml\n@macro skipme\nx\n@end macro\n@include does-not-exist.texi\n@end ifhtml\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be(string.Empty);
        document.Macros.ContainsKey("skipme").Should().BeFalse();
        document.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Process_conditionals_inside_macro_bodies_evaluate_at_expansion_time()
    {
        //Arrange
        string source =
            "@macro pick\n@ifset FDL\nfdl-yes\n@end ifset\n@ifclear FDL\nfdl-no\n@end ifclear\n@end macro\n"
            + "@set FDL\n@pick{}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("fdl-yes\n\n");
    }

    [Fact]
    public void Process_unterminated_conditional_warns()
    {
        //Arrange
        string source = "@iftex\nx\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.Warnings.Any(w => w.Category == TexinfoWarningCategory.Conditional).Should().BeTrue();
    }

    // ----- Raw blocks and comments -------------------------------------------------------------

    [Fact]
    public void Process_tex_and_html_raw_blocks_are_skipped_with_warnings()
    {
        //Arrange
        string source = "@tex\n\\gdef\\x{1}\n@end tex\nkeep\n@html\n<div>\n@end html\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("keep\n");
        document.Warnings.Count.Should().Be(2);
        document.Warnings.All(w => w.Category == TexinfoWarningCategory.RawBlockSkipped).Should().BeTrue();
    }

    [Fact]
    public void Process_ignore_blocks_disappear_silently()
    {
        //Arrange
        string source = "@ignore\nanything at all\n@end ignore\nkeep\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("keep\n");
        document.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Process_verbatim_blocks_pass_through_untouched()
    {
        //Arrange
        string source = "@verbatim\n@macro not-processed\n@end verbatim\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be(source);
    }

    [Fact]
    public void Process_comments_vanish_without_leaving_blank_lines()
    {
        //Arrange
        string source = "one\n@c gone\ntwo @c also gone\nthree\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("one\ntwo \nthree\n");
    }

    [Fact]
    public void Process_documentencoding_is_recorded_and_consumed()
    {
        //Arrange
        string source = "@documentencoding UTF-8\nbody\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DocumentEncoding.Should().Be("UTF-8");
        document.DumpExpandedSource().Should().Be("body\n");
        document.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Process_non_utf8_documentencoding_warns()
    {
        //Arrange
        string source = "@documentencoding ISO-8859-1\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.Warnings.Count.Should().Be(1);
        document.Warnings[0].Category.Should().Be(TexinfoWarningCategory.Encoding);
    }

    // ----- @include ----------------------------------------------------------------------------

    [Fact]
    public void ProcessFile_include_resolves_relative_to_parent_directory_like_lilypond()
    {
        //Arrange: driver and en/ live in one root; en/macros.itexi includes en/common.itexi,
        //which only resolves through the root directory (the LilyPond include pattern).
        string root = Directory.CreateTempSubdirectory("texinfo-test-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "en"));
            File.WriteAllText(Path.Combine(root, "en", "macros.itexi"),
                "@include en/common.itexi\nfrom-macros\n");
            File.WriteAllText(Path.Combine(root, "en", "common.itexi"), "from-common\n");
            string driver = Path.Combine(root, "driver.texi");
            File.WriteAllText(driver, "@include en/macros.itexi\ndone\n");

            //Act
            PreprocessedDocument document =
                new TexinfoPreprocessor(new PreprocessorOptions()).ProcessFile(driver);

            //Assert
            document.DumpExpandedSource().Should().Be("from-common\nfrom-macros\ndone\n");
            document.Warnings.Count.Should().Be(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProcessFile_missing_include_warns_and_continues()
    {
        //Arrange
        string root = Directory.CreateTempSubdirectory("texinfo-test-").FullName;
        try
        {
            string driver = Path.Combine(root, "driver.texi");
            File.WriteAllText(driver, "@include nowhere.texi\nstill-here\n");

            //Act
            PreprocessedDocument document =
                new TexinfoPreprocessor(new PreprocessorOptions()).ProcessFile(driver);

            //Assert
            document.DumpExpandedSource().Should().Be("still-here\n");
            document.Warnings.Count.Should().Be(1);
            document.Warnings[0].Category.Should().Be(TexinfoWarningCategory.Include);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProcessFile_include_cycle_warns_instead_of_hanging()
    {
        //Arrange
        string root = Directory.CreateTempSubdirectory("texinfo-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "a.texi"), "a-start\n@include b.texi\na-end\n");
            File.WriteAllText(Path.Combine(root, "b.texi"), "b-start\n@include a.texi\nb-end\n");

            //Act
            PreprocessedDocument document = new TexinfoPreprocessor(new PreprocessorOptions())
                .ProcessFile(Path.Combine(root, "a.texi"));

            //Assert
            document.DumpExpandedSource().Should().Be("a-start\nb-start\nb-end\na-end\n");
            document.Warnings.Count.Should().Be(1);
            document.Warnings[0].Category.Should().Be(TexinfoWarningCategory.Include);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProcessFile_extra_search_paths_are_honored()
    {
        //Arrange
        string root = Directory.CreateTempSubdirectory("texinfo-test-").FullName;
        string elsewhere = Directory.CreateTempSubdirectory("texinfo-elsewhere-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(elsewhere, "shared.itexi"), "shared-content\n");
            string driver = Path.Combine(root, "driver.texi");
            File.WriteAllText(driver, "@include shared.itexi\n");
            PreprocessorOptions options = new PreprocessorOptions();
            options.IncludeSearchPaths.Add(elsewhere);

            //Act
            PreprocessedDocument document = new TexinfoPreprocessor(options).ProcessFile(driver);

            //Assert
            document.DumpExpandedSource().Should().Be("shared-content\n");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    // ----- Alias -------------------------------------------------------------------------------

    [Fact]
    public void Process_alias_of_builtin_command_renames_the_invocation()
    {
        //Arrange
        string source = "@alias xyz=code\nuse @xyz{it} now\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("use @code{it} now\n");
    }

    [Fact]
    public void Process_alias_of_macro_expands_the_macro()
    {
        //Arrange
        string source = "@macro real{T}\n<\\T\\>\n@end macro\n@alias other=real\n@other{x}\n";

        //Act
        string dump = Dump(source);

        //Assert
        dump.Should().Be("<x>\n");
    }

    [Fact]
    public void Process_alias_shadowing_builtin_is_rejected_with_warning()
    {
        //Arrange
        string source = "@alias item=code\n@item one\n";

        //Act
        PreprocessedDocument document = Process(source);

        //Assert
        document.DumpExpandedSource().Should().Be("@item one\n");
        document.Warnings.Count.Should().Be(1);
        document.Warnings[0].Category.Should().Be(TexinfoWarningCategory.Macro);
    }
}
