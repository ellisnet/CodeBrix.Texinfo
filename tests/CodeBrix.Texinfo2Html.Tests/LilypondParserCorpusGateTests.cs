using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Parsing;
using CodeBrix.Texinfo2Html.Preprocessing;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The parser's corpus gate: every manual of the English LilyPond documentation must parse into a
/// document tree with no structural errors and only the warnings that a source checkout is
/// expected to produce. The corpus is read locally from ~/GitHome/lilypond and never committed;
/// these tests skip cleanly when it is not present.
/// </summary>
/// <remarks>
/// Three warning kinds are expected and nothing else is tolerated. Raw <c>@tex</c> blocks are
/// skipped by design. One macro warning is the deliberate refusal to let LilyPond's TeX-branch
/// <c>@macro cindex</c> shadow the built-in. Missing includes are files the LilyPond build
/// generates - version.itexi, the snippets, the notation appendix tables - which simply do not
/// exist in a source checkout.
/// </remarks>
public class LilypondParserCorpusGateTests
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
    /// Parses one manual in place, with a temporary stand-in for the build-generated
    /// version.itexi that macros.itexi includes.
    /// </summary>
    private static TexinfoDocument ParseManual(string manualFileName)
    {
        string standIn = Directory.CreateTempSubdirectory("texinfo-corpus-parse-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(standIn, "version.itexi"),
                "@c Test stand-in for LilyPond's build-generated version.itexi.\n"
                + "@macro version\n2.25.99\n@end macro\n"
                + "@macro versionStable\n2.24.99\n@end macro\n"
                + "@macro versionDevel\n2.25.99\n@end macro\n");
            PreprocessorOptions options = new PreprocessorOptions();
            options.IncludeSearchPaths.Add(standIn);
            PreprocessedDocument preprocessed = new TexinfoPreprocessor(options)
                .ProcessFile(Path.Combine(CorpusDocumentationRoot, "en", manualFileName));
            return new TexinfoParser(preprocessed).Parse();
        }
        finally
        {
            Directory.Delete(standIn, recursive: true);
        }
    }

    private static void AssertNoStructuralWarnings(TexinfoDocument document)
    {
        foreach (TexinfoWarning warning in document.Warnings)
        {
            switch (warning.Category)
            {
                case TexinfoWarningCategory.RawBlockSkipped:
                    break;
                case TexinfoWarningCategory.Macro:
                    warning.Message.Contains("cindex").Should().BeTrue();
                    break;
                case TexinfoWarningCategory.Include:
                    // Files the LilyPond build generates; absent from a source checkout.
                    warning.Message.Contains("was not found on the search path").Should().BeTrue();
                    break;
                default:
                    // Syntax, UnknownCommand, Reference, Value, Conditional and Encoding warnings
                    // all mean the parser met something it could not account for.
                    warning.ToString().Should().Be("no unexpected warning");
                    break;
            }
        }
        document.Warnings.Count(w => w.Category == TexinfoWarningCategory.Macro).Should().Be(1);
    }

    [Theory]
    [InlineData("changes.tely", "LilyPond Changes", 10, false)]
    [InlineData("essay.tely", "Essay on automated music engraving", 20, true)]
    [InlineData("extending.tely", "Extending LilyPond", 70, false)]
    [InlineData("learning.tely", "LilyPond Learning Manual", 165, false)]
    [InlineData("music-glossary.tely", "LilyPond Music Glossary", 340, false)]
    [InlineData("usage.tely", "LilyPond Application Usage", 70, false)]
    [InlineData("notation.tely", "LilyPond Notation Reference", 550, true)]
    [InlineData("snippets.tely", "LilyPond snippets", 5, true)]
    public void Manual_parses_with_only_expected_warnings(string manualFileName, string title,
        int minimumSections, bool hasBuildGeneratedIncludes)
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        TexinfoDocument document = ParseManual(manualFileName);

        //Assert
        AssertNoStructuralWarnings(document);
        DocumentInvariants.AssertAll(document);
        document.Title.Should().Be(title);
        document.AllSections().Count().Should().BeGreaterThanOrEqualTo(minimumSections);
        document.Anchors.ContainsKey("Top").Should().BeTrue();
        int missingIncludes = document.Warnings.Count(w => w.Category == TexinfoWarningCategory.Include);
        if (!hasBuildGeneratedIncludes)
        {
            missingIncludes.Should().Be(0);
        }
    }

    [Fact]
    public void Whole_corpus_parses_without_a_single_structural_warning()
    {
        SkipUnlessCorpusPresent();

        //Arrange
        string[] manuals =
        {
            "changes.tely", "essay.tely", "extending.tely", "learning.tely",
            "music-glossary.tely", "usage.tely", "notation.tely", "snippets.tely"
        };
        List<string> unexpected = new List<string>();
        int sections = 0;
        int indexEntries = 0;
        int deepestLevel = 0;

        //Act
        foreach (string manual in manuals)
        {
            TexinfoDocument document = ParseManual(manual);
            sections += document.AllSections().Count();
            indexEntries += document.IndexEntries.Count;
            deepestLevel = Math.Max(deepestLevel, document.AllSections().Max(s => s.Level));
            foreach (TexinfoWarning warning in document.Warnings)
            {
                bool expected = warning.Category == TexinfoWarningCategory.RawBlockSkipped
                    || warning.Category == TexinfoWarningCategory.Include
                    || (warning.Category == TexinfoWarningCategory.Macro
                        && warning.Message.Contains("cindex"));
                if (!expected)
                {
                    unexpected.Add($"{manual}: {warning}");
                }
            }
        }

        //Assert
        string.Join(Environment.NewLine, unexpected).Should().Be(string.Empty);
        sections.Should().BeGreaterThanOrEqualTo(1250);
        indexEntries.Should().BeGreaterThanOrEqualTo(4500);
        deepestLevel.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Notation_manual_builds_every_structure_the_emitter_has_to_render()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        TexinfoDocument document = ParseManual("notation.tely");

        //Assert - the largest manual exercises every structure the emitter will meet.
        document.IndexEntries.Count.Should().BeGreaterThanOrEqualTo(3800);
        document.Anchors.Count.Should().BeGreaterThan(document.AllSections().Count());
        document.Footnotes.Count.Should().BeGreaterThan(0);
        //The notation manual's deepest sectioning command is @subsection; @unnumberedsubsubsec
        //appears in the learning manual, and the whole-corpus test checks that depth.
        document.AllSections().Max(s => s.Level).Should().Be(3);

        List<TexinfoNode> all = document.AllNodes().ToList();
        all.OfType<MusicSnippetNode>().Count().Should().BeGreaterThan(500);
        all.OfType<MultitableNode>().Count().Should().BeGreaterThan(30);
        all.OfType<TableNode>().Count().Should().BeGreaterThan(10);
        all.OfType<ListNode>().Count().Should().BeGreaterThan(50);
        all.OfType<PreformattedNode>().Count().Should().BeGreaterThan(200);
        all.OfType<CrossReferenceNode>().Count().Should().BeGreaterThan(1000);
        all.OfType<MenuNode>().Count().Should().BeGreaterThan(0);
        all.OfType<UnknownCommandNode>().Count().Should().Be(0);
    }
}
