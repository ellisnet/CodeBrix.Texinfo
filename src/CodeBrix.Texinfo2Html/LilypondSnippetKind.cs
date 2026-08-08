namespace CodeBrix.Texinfo2Html;

/// <summary>
/// Which of the lilypond-book music environments a snippet was written as. The three differ in
/// where the music comes from, which is the first thing a renderer has to know.
/// </summary>
public enum LilypondSnippetKind
{
    /// <summary>
    /// An <c>@lilypond</c> environment, whose music source is written in the document itself -
    /// either as a block ending at <c>@end lilypond</c> or in braces inside a paragraph.
    /// </summary>
    Music,

    /// <summary>An <c>@lilypondfile</c> command, naming a LilyPond file to engrave.</summary>
    LilypondFile,

    /// <summary>An <c>@musicxmlfile</c> command, naming a MusicXML file to convert and engrave.</summary>
    MusicXmlFile
}
