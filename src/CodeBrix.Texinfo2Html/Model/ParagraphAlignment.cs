namespace CodeBrix.Texinfo2Html.Model;

/// <summary>How a paragraph's lines are aligned.</summary>
internal enum ParagraphAlignment
{
    /// <summary>The alignment the surrounding context implies.</summary>
    Default,

    /// <summary>Centered, from a <c>@center</c> line.</summary>
    Centered
}
