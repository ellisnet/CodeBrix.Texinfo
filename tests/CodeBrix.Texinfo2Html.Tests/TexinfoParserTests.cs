using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Parsing;
using CodeBrix.Texinfo2Html.Preprocessing;
using CodeBrix.Texinfo2Html.Sources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// Unit tests for <see cref="TexinfoParser"/>. Every document parsed here is also run through
/// <see cref="DocumentInvariants"/>, so the structural rules are checked on each fixture rather
/// than only on the corpus.
/// </summary>
public class TexinfoParserTests
{
    private static TexinfoDocument Parse(string source)
    {
        PreprocessedDocument preprocessed = new TexinfoPreprocessor(new PreprocessorOptions())
            .Process(TexinfoSourceText.FromString("test.texi", source), null);
        TexinfoDocument document = new TexinfoParser(preprocessed).Parse();
        DocumentInvariants.AssertAll(document);
        return document;
    }

    private static T Node<T>(TexinfoNode node) where T : TexinfoNode
    {
        (node is T).Should().BeTrue();
        return (T)node;
    }

    private static string TextOf(TexinfoNode node)
        => InlineNodes.ToPlainText(new[] { node });

    private static string TextOf(IReadOnlyList<TexinfoNode> nodes) => InlineNodes.ToPlainText(nodes);

    private static int CountOf(TexinfoDocument document, TexinfoWarningCategory category)
        => document.Warnings.Count(w => w.Category == category);

    [Fact]
    public void Parse_builds_one_paragraph_from_consecutive_lines()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("First line\nsecond line.\n");

        //Assert
        document.Preamble.Count.Should().Be(1);
        TextOf(document.Preamble[0]).Should().Be("First line\nsecond line.");
    }

    [Fact]
    public void Parse_splits_paragraphs_at_a_blank_line()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("One.\n\nTwo.\n");

        //Assert
        document.Preamble.Count.Should().Be(2);
        TextOf(document.Preamble[0]).Should().Be("One.");
        TextOf(document.Preamble[1]).Should().Be("Two.");
    }

    [Fact]
    public void Parse_nests_sections_by_their_level()
    {
        //Arrange
        const string source = "@top Manual\n@chapter One\n@section A\n@chapter Two\n";

        //Act
        TexinfoDocument document = Parse(source);

        //Assert
        document.Sections.Count.Should().Be(1);
        SectionNode top = document.Sections[0];
        top.Level.Should().Be(0);
        top.Kind.Should().Be(SectionKind.Top);
        top.Children.Count.Should().Be(2);
        top.Children[0].Children.Count.Should().Be(1);
        top.Children[0].Children[0].Level.Should().Be(2);
        TextOf(top.Children[0].Children[0].Title).Should().Be("A");
        top.Children[1].Children.Count.Should().Be(0);
    }

    [Fact]
    public void Parse_gives_appendix_and_unnumbered_commands_their_own_kind()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@unnumbered Preface\n@appendix Tables\n@appendixsec Details\n");

        //Assert
        document.Sections[0].Kind.Should().Be(SectionKind.Unnumbered);
        document.Sections[1].Kind.Should().Be(SectionKind.Appendix);
        document.Sections[1].Children[0].Kind.Should().Be(SectionKind.Appendix);
        document.Sections[1].Children[0].Level.Should().Be(2);
    }

    [Fact]
    public void Parse_attaches_a_preceding_node_name_to_its_section()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@node Getting Started, Next, Previous, Top\n@chapter Getting Started\n");

        //Assert
        SectionNode section = document.Sections[0];
        section.NodeName.Should().Be("Getting Started");
        document.Anchors["Getting Started"].Target.Should().Be(section);
        document.Anchors["Getting Started"].Kind.Should().Be(TexinfoAnchorKind.Node);
    }

    [Fact]
    public void Parse_keeps_a_node_without_a_section_as_a_standalone_marker()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@node Lonely\n\nSome text.\n");

        //Assert
        NodeAnchorNode marker = Node<NodeAnchorNode>(document.Preamble[0]);
        marker.NodeName.Should().Be("Lonely");
        document.Anchors["Lonely"].Target.Should().Be(marker);
        TextOf(document.Preamble[1]).Should().Be("Some text.");
    }

    [Fact]
    public void Parse_records_inline_commands_with_their_semantic_style()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("Use @code{\\relative} and @emph{care}.\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        InlineCommandNode code = Node<InlineCommandNode>(paragraph.Content[1]);
        code.Style.Should().Be(InlineStyle.Code);
        InlineNodes.ToPlainText(code.Content).Should().Be("\\relative");
        Node<InlineCommandNode>(paragraph.Content[3]).Style.Should().Be(InlineStyle.Emphasis);
    }

    [Fact]
    public void Parse_resolves_glyph_commands_to_their_text()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("Wait@dots{} 5@minus{}3 @copyright{} @TeX{}\n");

        //Assert
        TextOf(document.Preamble[0]).Should().Be("Wait… 5−3 © TeX");
    }

    [Fact]
    public void Parse_reads_a_cross_reference_with_all_five_arguments()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("See @ref{Slur,,Slurs,internals,Internals Reference} now.\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        CrossReferenceNode reference = Node<CrossReferenceNode>(paragraph.Content[1]);
        reference.Kind.Should().Be(CrossReferenceKind.Reference);
        reference.NodeName.Should().Be("Slur");
        reference.Label.Should().Be(string.Empty);
        InlineNodes.ToPlainText(reference.Title).Should().Be("Slurs");
        reference.InfoFile.Should().Be("internals");
        reference.Manual.Should().Be("Internals Reference");
        reference.IsExternal.Should().BeTrue();
    }

    [Fact]
    public void Parse_distinguishes_the_three_reference_commands()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@xref{A}. Text @pxref{B}.\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        Node<CrossReferenceNode>(paragraph.Content[0]).Kind.Should().Be(CrossReferenceKind.SentenceStart);
        Node<CrossReferenceNode>(paragraph.Content[2]).Kind.Should().Be(CrossReferenceKind.Parenthetical);
    }

    [Fact]
    public void Parse_protects_a_comma_written_as_the_comma_command()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@ref{World Music@comma{} Arabic}\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        Node<CrossReferenceNode>(paragraph.Content[0]).NodeName.Should().Be("World Music, Arabic");
    }

    [Fact]
    public void Parse_reads_links_and_mail_addresses()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@uref{https://lilypond.org, LilyPond} @email{bug@@example.org}\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        LinkNode url = Node<LinkNode>(paragraph.Content[0]);
        url.Kind.Should().Be(LinkKind.Url);
        url.Target.Should().Be("https://lilypond.org");
        InlineNodes.ToPlainText(url.Text).Should().Be("LilyPond");
        LinkNode mail = Node<LinkNode>(paragraph.Content[2]);
        mail.Kind.Should().Be(LinkKind.Email);
        mail.Target.Should().Be("bug@example.org");
    }

    [Fact]
    public void Parse_numbers_footnotes_in_document_order()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("One@footnote{First note.} two@footnote{Second note.}\n");

        //Assert
        document.Footnotes.Count.Should().Be(2);
        document.Footnotes[0].Number.Should().Be(1);
        InlineNodes.ToPlainText(document.Footnotes[0].Content).Should().Be("First note.");
        document.Footnotes[1].Number.Should().Be(2);
    }

    [Fact]
    public void Parse_collects_index_entries_with_the_section_they_belong_to()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@chapter Notes\n@cindex slur\n@findex \\slur\n");

        //Assert
        document.IndexEntries.Count.Should().Be(2);
        document.IndexEntries[0].IndexName.Should().Be("cp");
        InlineNodes.ToPlainText(document.IndexEntries[0].Content).Should().Be("slur");
        document.IndexEntries[0].Section.Should().Be(document.Sections[0]);
        document.IndexEntries[1].IndexName.Should().Be("fn");
    }

    [Fact]
    public void Parse_takes_the_sort_key_from_sortas_and_drops_it_from_the_entry()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@findex @sortas{relative} \\relative\n");

        //Assert
        IndexEntryNode entry = document.IndexEntries[0];
        entry.SortKey.Should().Be("relative");
        InlineNodes.ToPlainText(entry.Content).Should().Be("\\relative");
    }

    [Fact]
    public void Parse_keeps_an_index_entry_inside_an_open_paragraph()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("Text before.\n@cindex middle\nText after.\n");

        //Assert - the entry must not split the paragraph in two.
        document.Preamble.Count.Should().Be(1);
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        paragraph.Content.Any(n => n is IndexEntryNode).Should().BeTrue();
        TextOf(paragraph).Should().Be("Text before.\nText after.");
    }

    [Fact]
    public void Parse_preserves_line_breaks_inside_a_preformatted_block()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@example\nline one\n  line two\n@end example\n");

        //Assert
        PreformattedNode preformatted = Node<PreformattedNode>(document.Preamble[0]);
        preformatted.Kind.Should().Be(TexinfoBlockKind.Example);
        InlineNodes.ToPlainText(preformatted.Content).Should().Be("line one\n  line two\n");
    }

    [Fact]
    public void Parse_treats_group_inside_a_preformatted_block_as_transparent()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@example\n@group\nc d e\n@end group\n@end example\n");

        //Assert
        PreformattedNode preformatted = Node<PreformattedNode>(document.Preamble[0]);
        InlineNodes.ToPlainText(preformatted.Content).Should().Be("c d e\n");
    }

    [Fact]
    public void Parse_reads_an_itemize_list_with_its_bullet()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@itemize @bullet\n@item First\n@item Second\n@end itemize\n");

        //Assert
        ListNode list = Node<ListNode>(document.Preamble[0]);
        list.IsEnumerated.Should().BeFalse();
        list.Marker.Should().Be("bullet");
        list.Items.Count.Should().Be(2);
        TextOf(list.Items[1]).Should().Be("Second");
    }

    [Fact]
    public void Parse_reads_an_enumerate_list_with_its_starting_value()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@enumerate a\n@item Alpha\n\nStill alpha.\n@item Beta\n@end enumerate\n");

        //Assert
        ListNode list = Node<ListNode>(document.Preamble[0]);
        list.IsEnumerated.Should().BeTrue();
        list.Marker.Should().Be("a");
        list.Items[0].Blocks.Count.Should().Be(2);
    }

    [Fact]
    public void Parse_reads_a_table_and_groups_itemx_terms_with_one_description()
    {
        //Arrange
        const string source = "@table @code\n@item foo\n@itemx bar\nDescribes both.\n@item baz\nOnly baz.\n@end table\n";

        //Act
        TexinfoDocument document = Parse(source);

        //Assert
        TableNode table = Node<TableNode>(document.Preamble[0]);
        table.FormatCommand.Should().Be("code");
        table.Entries.Count.Should().Be(2);
        table.Entries[0].Terms.Count.Should().Be(2);
        table.Entries[0].Terms[1].IsContinuation.Should().BeTrue();
        TextOf(table.Entries[0].Terms[1]).Should().Be("bar");
        InlineNodes.ToPlainText(table.Entries[0].Blocks).Should().Be("Describes both.");
        table.Entries[1].Terms.Count.Should().Be(1);
    }

    [Fact]
    public void Parse_files_ftable_terms_in_the_function_index()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@ftable @code\n@item one\n@item two\n@end ftable\n");

        //Assert - the index entry is the only thing that makes @ftable more than @table, so the
        //table's index name and an entry per term are two halves of the same claim.
        Node<TableNode>(document.Preamble[0]).IndexName.Should().Be("fn");
        document.IndexEntries.Count.Should().Be(2);
        document.IndexEntries[0].IndexName.Should().Be("fn");
        InlineNodes.ToPlainText(document.IndexEntries[0].Content).Should().Be("one");
        InlineNodes.ToPlainText(document.IndexEntries[1].Content).Should().Be("two");
    }

    [Fact]
    public void Parse_files_vtable_terms_in_the_variable_index()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@vtable @code\n@item alpha\n@end vtable\n");

        //Assert
        document.IndexEntries.Count.Should().Be(1);
        document.IndexEntries[0].IndexName.Should().Be("vr");
    }

    [Fact]
    public void Parse_files_no_index_entries_for_a_plain_table()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@table @code\n@item one\n@end table\n");

        //Assert
        document.IndexEntries.Count.Should().Be(0);
    }

    [Fact]
    public void Parse_reads_a_multitable_with_column_fractions_and_a_header_row()
    {
        //Arrange
        const string source = "@multitable @columnfractions .3 .7\n"
            + "@headitem Name @tab Meaning\n"
            + "@item a @tab first\n"
            + "@item b @tab second\n"
            + "@end multitable\n";

        //Act
        TexinfoDocument document = Parse(source);

        //Assert
        MultitableNode table = Node<MultitableNode>(document.Preamble[0]);
        table.ColumnFractions.Count.Should().Be(2);
        table.ColumnFractions[1].Should().Be(0.7);
        table.ColumnCount.Should().Be(2);
        table.Rows.Count.Should().Be(3);
        table.Rows[0].IsHeader.Should().BeTrue();
        table.Rows[1].IsHeader.Should().BeFalse();
        table.Rows[1].Cells.Count.Should().Be(2);
        TextOf(table.Rows[1].Cells[1]).Should().Be("first");
    }

    [Fact]
    public void Parse_reads_multitable_column_prototypes()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@multitable {wide heading} {b}\n@item x @tab y\n@end multitable\n");

        //Assert
        MultitableNode table = Node<MultitableNode>(document.Preamble[0]);
        table.ColumnPrototypes.Count.Should().Be(2);
        table.ColumnPrototypes[0].Should().Be("wide heading");
    }

    [Fact]
    public void Parse_keeps_a_verbatim_block_uninterpreted()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@verbatim\n@code{not a command}\n@end verbatim\n");

        //Assert
        Node<VerbatimNode>(document.Preamble[0]).Text.Should().Be("@code{not a command}\n");
    }

    [Fact]
    public void Parse_reads_a_block_form_music_snippet()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@lilypond[quote,verbatim]\n{ c d e }\n@end lilypond\n");

        //Assert
        MusicSnippetNode snippet = Node<MusicSnippetNode>(document.Preamble[0]);
        snippet.CommandName.Should().Be("lilypond");
        snippet.RawOptions.Should().Be("[quote,verbatim]");
        snippet.Content.Should().Be("{ c d e }\n");
        snippet.IsInlineForm.Should().BeFalse();
        snippet.IsFileReference.Should().BeFalse();
    }

    [Fact]
    public void Parse_keeps_an_inline_music_snippet_inside_its_paragraph()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("A note @lilypond[inline]{c'4} in text.\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        MusicSnippetNode snippet = Node<MusicSnippetNode>(paragraph.Content[1]);
        snippet.IsInlineForm.Should().BeTrue();
        snippet.Content.Should().Be("c'4");
    }

    [Fact]
    public void Parse_reads_lilypondfile_with_its_brace_group_on_the_next_line()
    {
        //Arrange - the corpus writes the option list and the file name on separate lines, and
        //sometimes puts a space before the option list.
        const string source = "@lilypondfile [verbatim, quote]\n{snippets/example.ly}\n";

        //Act
        TexinfoDocument document = Parse(source);

        //Assert
        MusicSnippetNode snippet = Node<MusicSnippetNode>(document.Preamble[0]);
        snippet.CommandName.Should().Be("lilypondfile");
        snippet.Content.Should().Be("snippets/example.ly");
        snippet.IsFileReference.Should().BeTrue();
        document.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Parse_records_a_menu_and_its_entries()
    {
        //Arrange
        const string source = "@menu\n* First::       The first node.\n"
            + "* Label: Second Node.   Its description.\n@end menu\n";

        //Act
        TexinfoDocument document = Parse(source);

        //Assert
        MenuNode menu = Node<MenuNode>(document.Preamble[0]);
        menu.Entries.Count.Should().Be(2);
        menu.Entries[0].NodeName.Should().Be("First");
        menu.Entries[0].Description.Should().Be("The first node.");
        menu.Entries[1].Label.Should().Be("Label");
        menu.Entries[1].NodeName.Should().Be("Second Node");
    }

    [Fact]
    public void Parse_records_the_document_title_and_language()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@settitle My Manual\n@documentlanguage en\n@afourpaper\n");

        //Assert
        document.Title.Should().Be("My Manual");
        document.Language.Should().Be("en");
        document.Settings.ContainsKey("afourpaper").Should().BeTrue();
    }

    [Fact]
    public void Parse_moves_copying_content_onto_the_document()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@copying\nThe notice.\n@end copying\n@insertcopying\n");

        //Assert
        InlineNodes.ToPlainText(document.Copying).Should().Be("The notice.");
        Node<DirectiveNode>(document.Preamble[0]).Kind.Should().Be(DirectiveKind.InsertCopying);
    }

    [Fact]
    public void Parse_records_index_merges_and_print_requests()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@syncodeindex fn cp\n@contents\n@printindex cp\n");

        //Assert
        document.IndexMerges.Count.Should().Be(1);
        document.IndexMerges[0].SourceIndex.Should().Be("fn");
        document.IndexMerges[0].TargetIndex.Should().Be("cp");
        document.IndexMerges[0].UseCodeFont.Should().BeTrue();
        Node<DirectiveNode>(document.Preamble[0]).Kind.Should().Be(DirectiveKind.Contents);
        DirectiveNode print = Node<DirectiveNode>(document.Preamble[1]);
        print.Kind.Should().Be(DirectiveKind.PrintIndex);
        print.Argument.Should().Be("cp");
    }

    [Fact]
    public void Parse_reads_an_image_with_all_of_its_arguments()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@image{pictures/logo, 5cm, 3cm, The logo, png}\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        ImageNode image = Node<ImageNode>(paragraph.Content[0]);
        image.FileName.Should().Be("pictures/logo");
        image.Width.Should().Be("5cm");
        image.Height.Should().Be("3cm");
        image.AlternateText.Should().Be("The logo");
        image.Extension.Should().Be("png");
    }

    [Fact]
    public void Parse_applies_noindent_to_the_paragraph_that_follows_it()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("First.\n\n@noindent\nSecond.\n");

        //Assert
        Node<ParagraphNode>(document.Preamble[0]).SuppressIndent.Should().BeFalse();
        Node<ParagraphNode>(document.Preamble[1]).SuppressIndent.Should().BeTrue();
    }

    [Fact]
    public void Parse_centers_a_center_line()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@center A dedication\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        paragraph.Alignment.Should().Be(ParagraphAlignment.Centered);
        TextOf(paragraph).Should().Be("A dedication");
    }

    [Fact]
    public void Parse_treats_the_heading_family_as_blocks_rather_than_sections()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@chapter Real\n@subheading Just a heading\n");

        //Assert
        document.Sections.Count.Should().Be(1);
        document.Sections[0].Children.Count.Should().Be(0);
        HeadingNode heading = Node<HeadingNode>(document.Sections[0].Blocks[0]);
        heading.Kind.Should().Be(HeadingKind.Subsection);
        heading.Level.Should().Be(3);
    }

    [Fact]
    public void Parse_reads_a_quotation_with_its_label()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@quotation Note\nBe careful.\n@end quotation\n");

        //Assert
        BlockEnvironmentNode quotation = Node<BlockEnvironmentNode>(document.Preamble[0]);
        quotation.Kind.Should().Be(TexinfoBlockKind.Quotation);
        InlineNodes.ToPlainText(quotation.Argument).Should().Be("Note");
        InlineNodes.ToPlainText(quotation.Blocks).Should().Be("Be careful.");
    }

    [Fact]
    public void Parse_registers_an_anchor_as_a_named_destination()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@chapter One\n@anchor{Special Spot}\nText.\n");

        //Assert
        document.Anchors.ContainsKey("Special Spot").Should().BeTrue();
        document.Anchors["Special Spot"].Kind.Should().Be(TexinfoAnchorKind.Anchor);
    }

    [Fact]
    public void Parse_stops_at_bye()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("Kept.\n@bye\nDiscarded.\n");

        //Assert
        document.Preamble.Count.Should().Be(1);
        TextOf(document.Preamble[0]).Should().Be("Kept.");
    }

    [Fact]
    public void Parse_warns_about_an_unknown_command_but_keeps_its_argument()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("A @frobnicate{kept text} B\n");

        //Assert
        CountOf(document, TexinfoWarningCategory.UnknownCommand).Should().Be(1);
        TextOf(document.Preamble[0]).Should().Be("A kept text B");
    }

    [Fact]
    public void Parse_keeps_an_unsupported_environment_as_a_plain_block()
    {
        //Arrange + Act - an environment nothing implements, recognized as one purely because a
        //matching '@end' follows it.
        TexinfoDocument document = Parse("@sidebar Aside\nWhat it says.\n@end sidebar\n");

        //Assert
        CountOf(document, TexinfoWarningCategory.UnknownCommand).Should().Be(1);
        BlockEnvironmentNode block = Node<BlockEnvironmentNode>(document.Preamble[0]);
        block.Kind.Should().Be(TexinfoBlockKind.Unknown);
        InlineNodes.ToPlainText(block.Blocks).Should().Be("What it says.");
    }

    [Fact]
    public void Parse_treats_an_unknown_command_with_no_matching_end_as_inline()
    {
        //Arrange + Act - the same name without an '@end' is not an environment, and swallowing the
        //rest of the document looking for one would be far worse than losing the command.
        TexinfoDocument document = Parse("@sidebar\nOrdinary text.\n\nA second paragraph.\n");

        //Assert
        CountOf(document, TexinfoWarningCategory.UnknownCommand).Should().Be(1);
        document.Preamble.Count.Should().Be(2);
        InlineNodes.ToPlainText(Node<ParagraphNode>(document.Preamble[1]).Content)
            .Should().Be("A second paragraph.");
    }

    [Fact]
    public void Parse_warns_about_a_missing_end_and_still_returns_the_block()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@example\nunterminated\n");

        //Assert
        CountOf(document, TexinfoWarningCategory.Syntax).Should().Be(1);
        Node<PreformattedNode>(document.Preamble[0]).Content.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Parse_warns_about_a_stray_end_and_carries_on()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("Before.\n@end example\nAfter.\n");

        //Assert
        CountOf(document, TexinfoWarningCategory.Syntax).Should().Be(1);
        document.Preamble.Count.Should().Be(2);
        TextOf(document.Preamble[1]).Should().Be("After.");
    }

    [Fact]
    public void Parse_recovers_when_an_inner_environment_is_never_closed()
    {
        //Arrange - the '@end itemize' has to close both the example and the list.
        const string source = "@itemize\n@item\n@example\nlost\n@end itemize\nAfter.\n";

        //Act
        TexinfoDocument document = Parse(source);

        //Assert
        CountOf(document, TexinfoWarningCategory.Syntax).Should().Be(1);
        Node<ListNode>(document.Preamble[0]).Items.Count.Should().Be(1);
        TextOf(document.Preamble[1]).Should().Be("After.");
    }

    [Fact]
    public void Parse_warns_when_a_node_name_is_used_twice()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("@node Same\n@chapter One\n@node Same\n@chapter Two\n");

        //Assert
        CountOf(document, TexinfoWarningCategory.Reference).Should().Be(1);
        document.Anchors.Count.Should().Be(1);
        document.Sections.Count.Should().Be(2);
    }

    [Fact]
    public void Parse_reads_a_verbatiminclude_resolved_by_the_preprocessor()
    {
        //Arrange
        string root = Directory.CreateTempSubdirectory("texinfo-parser-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "snippet.txt"), "@code{literal}\nsecond line\n");
            string driver = Path.Combine(root, "driver.texi");
            File.WriteAllText(driver, "@verbatiminclude snippet.txt\n");

            //Act
            PreprocessedDocument preprocessed =
                new TexinfoPreprocessor(new PreprocessorOptions()).ProcessFile(driver);
            TexinfoDocument document = new TexinfoParser(preprocessed).Parse();

            //Assert
            DocumentInvariants.AssertAll(document);
            Node<VerbatimNode>(document.Preamble[0]).Text.Should().Be("@code{literal}\nsecond line\n");
            document.Warnings.Count.Should().Be(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Parse_handles_the_single_character_commands()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("a@@b @{c@} d@*e f@ g\n");

        //Assert
        ParagraphNode paragraph = Node<ParagraphNode>(document.Preamble[0]);
        paragraph.Content.Any(n => n is LineBreakNode).Should().BeTrue();
        TextOf(paragraph).Should().Be("a@b {c} d e f g");
    }

    [Fact]
    public void Parse_drops_the_tex_input_line_that_opens_a_texinfo_file()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("\\input texinfo @c -*- coding: utf-8; -*-\n\n@settitle Manual\nBody.\n");

        //Assert
        document.Title.Should().Be("Manual");
        document.Preamble.Count.Should().Be(1);
        TextOf(document.Preamble[0]).Should().Be("Body.");
    }

    [Fact]
    public void Parse_keeps_a_backslash_line_that_is_not_the_tex_input_line()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("\\score is a LilyPond command.\n");

        //Assert
        TextOf(document.Preamble[0]).Should().Be("\\score is a LilyPond command.");
    }

    [Fact]
    public void Parse_leaves_an_empty_document_empty()
    {
        //Arrange + Act
        TexinfoDocument document = Parse("\n   \n\n");

        //Assert
        document.Preamble.Count.Should().Be(0);
        document.Sections.Count.Should().Be(0);
        document.Warnings.Count.Should().Be(0);
    }
}
