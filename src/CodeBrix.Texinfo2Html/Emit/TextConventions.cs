using System.Text;

namespace CodeBrix.Texinfo2Html.Emit;

/// <summary>
/// The typographic conventions Texinfo applies to ordinary running text: three hyphens are an em
/// dash, two are an en dash, and the ASCII quote characters are the directed quotation marks a
/// printed page uses.
/// </summary>
/// <remarks>
/// These conversions belong to text and not to code. The Texinfo manual is explicit about it -
/// hyphens "remain as they are in literal contexts, such as <c>@code</c> and <c>@example</c>", and
/// the backtick and the apostrophe render as themselves there - so the emitter tracks whether it is
/// inside such a context and only calls this class when it is not. A command-line option written as
/// <c>--verbose</c> inside <c>@code</c> would otherwise turn into an en dash and stop being an
/// option.
/// </remarks>
internal static class TextConventions
{
    /// <summary>
    /// Applies the conventions to a run of ordinary text. Text with nothing to convert is returned
    /// unchanged, which is the common case and worth the scan.
    /// </summary>
    /// <param name="text">The text as the document wrote it.</param>
    public static string Apply(string text)
    {
        if (string.IsNullOrEmpty(text) || !NeedsConversion(text))
        {
            return text;
        }
        StringBuilder builder = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            switch (c)
            {
                case '-':
                    i = AppendDashes(builder, text, i);
                    break;
                case '`':
                    if (i + 1 < text.Length && text[i + 1] == '`')
                    {
                        builder.Append('“');
                        i += 2;
                        break;
                    }
                    builder.Append('‘');
                    i++;
                    break;
                case '\'':
                    if (i + 1 < text.Length && text[i + 1] == '\'')
                    {
                        builder.Append('”');
                        i += 2;
                        break;
                    }
                    builder.Append('’');
                    i++;
                    break;
                default:
                    builder.Append(c);
                    i++;
                    break;
            }
        }
        return builder.ToString();
    }

    private static bool NeedsConversion(string text)
    {
        foreach (char c in text)
        {
            if (c == '-' || c == '`' || c == '\'')
            {
                return true;
            }
        }
        return false;
    }

    private static int AppendDashes(StringBuilder builder, string text, int start)
    {
        int end = start;
        while (end < text.Length && text[end] == '-')
        {
            end++;
        }
        //A run is read left to right the way TeX reads its ligatures: every three hyphens are an em
        //dash, a remaining pair is an en dash, and a lone hyphen stays a hyphen. Four hyphens are
        //therefore an em dash followed by a hyphen, not two en dashes.
        int remaining = end - start;
        while (remaining >= 3)
        {
            builder.Append('—');
            remaining -= 3;
        }
        if (remaining == 2)
        {
            builder.Append('–');
        }
        else if (remaining == 1)
        {
            builder.Append('-');
        }
        return end;
    }
}
