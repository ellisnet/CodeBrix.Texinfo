namespace CodeBrix.Texinfo2Html.Model;

/// <summary>How a paragraph sits within the block around it.</summary>
internal enum ParagraphAlignment
{
    /// <summary>The alignment the surrounding context implies.</summary>
    Default,

    /// <summary>Centered, from a <c>@center</c> line.</summary>
    Centered,

    /// <summary>
    /// Moved out to the left of the block containing it, from an <c>@exdent</c> line. The command
    /// exists to let one line of an indented environment stand clear of the indentation.
    /// </summary>
    Exdented
}
