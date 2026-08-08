using System;
using System.IO;
using System.Linq;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Preprocessing;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The macro engine's corpus gate: LilyPond's macros.itexi and common-macros.itexi (read locally from
/// ~/GitHome/lilypond, never committed) must expand correctly under the Print profile. These
/// tests skip cleanly when the corpus is not present on the machine.
/// </summary>
public class LilypondCorpusGateTests
{
    private static string CorpusDocumentationRoot
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "GitHome", "lilypond", "Documentation");

    private static void SkipUnlessCorpusPresent()
    {
        if (!File.Exists(Path.Combine(CorpusDocumentationRoot, "en", "macros.itexi")))
        {
            Assert.Skip($"LilyPond documentation corpus not present under {CorpusDocumentationRoot}.");
        }
    }

    /// <summary>
    /// Runs the preprocessor over a driver document that pulls in the real corpus macro files,
    /// with a local stand-in for the build-generated version.itexi.
    /// </summary>
    private static PreprocessedDocument ProcessDriver(string invocationLines)
    {
        string root = Directory.CreateTempSubdirectory("texinfo-corpus-gate-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "version.itexi"),
                "@c Test stand-in for LilyPond's build-generated version.itexi.\n"
                + "@macro version\n2.25.99\n@end macro\n"
                + "@macro versionStable\n2.24.99\n@end macro\n"
                + "@macro versionDevel\n2.25.99\n@end macro\n");
            string driver = Path.Combine(root, "driver.texi");
            File.WriteAllText(driver,
                "@set FDL\n"
                + "@macro manualIntro\nThis is the manual intro.\n@end macro\n"
                + "@macro copyrightDeclare\nCopyright @copyright{} 2026 by the test driver.\n@end macro\n"
                + "@include en/macros.itexi\n"
                + invocationLines);
            PreprocessorOptions options = new PreprocessorOptions();
            options.IncludeSearchPaths.Add(CorpusDocumentationRoot);
            return new TexinfoPreprocessor(options).ProcessFile(driver);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertOnlyExpectedWarnings(PreprocessedDocument document)
    {
        foreach (TexinfoWarning warning in document.Warnings)
        {
            bool isExpected = warning.Category == TexinfoWarningCategory.RawBlockSkipped
                || (warning.Category == TexinfoWarningCategory.Macro
                    && warning.Message.Contains("cindex"));
            isExpected.Should().BeTrue();
        }
        document.Warnings.Count(w => w.Category == TexinfoWarningCategory.Macro).Should().Be(1);
        document.Warnings.Count(w => w.Category == TexinfoWarningCategory.RawBlockSkipped)
            .Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public void Corpus_macro_files_define_the_expected_macro_table()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        PreprocessedDocument document = ProcessDriver(string.Empty);

        //Assert - the long tail of LilyPond commands all arrive through these definitions.
        string[] expectedMacros =
        {
            "q", "qq", "warning", "docMain", "lilyTitlePage", "predefined", "endpredefined",
            "snippets", "morerefs", "endmorerefs", "knownissues", "notation", "smallspace",
            "advanced", "subsubsubheading", "lydoctitle", "iref", "rmusicfn", "bs",
            "funindex", "funindexpre", "funindexpost", "untranslated", "tb", "tbsl",
            "pygments", "endPygments", "sourceimage", "staticFile",
            "rglos", "rglosnamed", "rlearning", "rlearningnamed", "rnotation", "rnotationnamed",
            "rchanges", "rchangesnamed", "rextend", "rextendnamed", "rcontrib", "rcontribnamed",
            "rweb", "rwebnamed", "ressay", "ressaynamed", "rprogram", "rprogramnamed",
            "rlsr", "rlsrnamed", "rlsrsnippet", "rinternals", "rinternalsnamed",
            "version", "versionStable", "versionDevel", "manualIntro", "copyrightDeclare"
        };
        foreach (string name in expectedMacros)
        {
            document.Macros.ContainsKey(name).Should().BeTrue();
        }

        //The @ifnottex definitions must have won over the TeX-only @iftex ones.
        document.Macros["funindex"].Body.Contains("@findex \\TEXT\\").Should().BeTrue();
        document.Macros["funindex"].Body.Contains("@indexC").Should().BeFalse();
        document.Macros["notation"].Body.Should().Be("@var{\\TEXT\\}");
        document.Macros["iref"].Body.Should().Be("@ref{\\TEXT\\}");
        document.Macros["advanced"].Body.Contains("@quotation").Should().BeTrue();
        document.Macros["warning"].Body.Contains("@cartouche").Should().BeTrue();
        document.Macros["untranslated"].Body.Should().Be(string.Empty);
        document.Macros["rinternals"].Body.Contains("internals,Internals Reference").Should().BeTrue();

        //The TeX-branch attempt to redefine the built-in @cindex must have been rejected.
        document.Macros.ContainsKey("cindex").Should().BeFalse();

        //Flags set by common-macros.itexi are recorded.
        document.Values.ContainsKey("txicodequoteundirected").Should().BeTrue();
        document.Values.ContainsKey("txiindexbackslashignore").Should().BeTrue();

        AssertOnlyExpectedWarnings(document);
    }

    [Fact]
    public void Corpus_macros_expand_representative_invocations_correctly()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        PreprocessedDocument document = ProcessDriver(
            "@q{hi}\n"
            + "@warning{Be careful.}\n"
            + "@notation{slur}\n"
            + "@funindex \\once\n"
            + "@funindex @sortas{@bs{}@bs{}} \\\\\n"
            + "@rinternals{Slur}\n"
            + "@rlsrsnippet{World Music, Arabic improvisation}\n"
            + "@rglosnamed{a, b}\n"
            + "@smallspace\n"
            + "For LilyPond version @version{}\n");
        string dump = document.DumpExpandedSource();

        //Assert
        dump.Contains("@quoteleft{}hi@quoteright{}").Should().BeTrue();
        dump.Contains("@cartouche\n@b{Note:} Be careful.\n@end cartouche").Should().BeTrue();
        dump.Contains("@var{slur}").Should().BeTrue();
        dump.Contains("@findex \\once\n").Should().BeTrue();
        dump.Contains("@findex @sortas{\\\\} \\\\\n").Should().BeTrue();
        dump.Contains("@ref{Slur,,,internals,Internals Reference}").Should().BeTrue();
        dump.Contains("@ref{World Music - Arabic improvisation,,Arabic improvisation,snippets,Snippets}")
            .Should().BeTrue();
        dump.Contains("@ref{a,,b,music-glossary,Music Glossary}").Should().BeTrue();
        dump.Contains("@sp 1\n").Should().BeTrue();
        dump.Contains("For LilyPond version 2.25.99").Should().BeTrue();

        //No blank line crept in between the two consecutive @funindex expansions.
        dump.Contains("@findex \\once\n@findex @sortas").Should().BeTrue();

        AssertOnlyExpectedWarnings(document);
    }

    [Fact]
    public void Corpus_lilyTitlePage_expands_with_fdl_copying_and_version()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        PreprocessedDocument document = ProcessDriver("@lilyTitlePage{Learning Manual}\n");
        string dump = document.DumpExpandedSource();

        //Assert - conditionals inside the body evaluated at expansion time, nested macro
        //invocations expanded, and the version stand-in reached through @include.
        dump.Contains("@top LilyPond --- Learning Manual").Should().BeTrue();
        dump.Contains("Permission is granted to copy").Should().BeTrue();
        dump.Contains("This is the manual intro.").Should().BeTrue();
        dump.Contains("Copyright @copyright{} 2026 by the test driver.").Should().BeTrue();
        dump.Contains("@insertcopying").Should().BeTrue();
        dump.Contains("For LilyPond version 2.25.99").Should().BeTrue();
        dump.Contains("This document has been placed in the public domain").Should().BeFalse();

        AssertOnlyExpectedWarnings(document);
    }
}
