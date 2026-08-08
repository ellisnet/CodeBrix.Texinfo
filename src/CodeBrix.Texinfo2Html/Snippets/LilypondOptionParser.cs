using System;
using System.Collections.Generic;
using System.Globalization;

namespace CodeBrix.Texinfo2Html.Snippets;

/// <summary>
/// Reads the bracketed option list of a lilypond-book music environment into
/// <see cref="LilypondSnippetOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The vocabulary here is the one measured across the whole English LilyPond documentation set -
/// every option those 110,000 lines actually use, and nothing invented beyond them. An option
/// outside it is not an error: it is recorded as written and reported once for the document, so a
/// renderer that knows more than this library can still act on it.
/// </para>
/// <para>
/// Values are kept as strings wherever they are LilyPond dimensions (<c>3\cm</c>, <c>6\in</c>).
/// They mean nothing outside an engraver, so converting them here could only lose information.
/// </para>
/// </remarks>
internal static class LilypondOptionParser
{
    /// <summary>Reads an option list, brackets and all, as the lexer captured it.</summary>
    /// <param name="rawOptions">
    /// The text the lexer kept for the environment's opening line. Anything outside the first
    /// bracketed group is ignored, because the lexer appends whatever else the line held.
    /// </param>
    public static LilypondSnippetOptions Parse(string rawOptions)
    {
        LilypondSnippetOptions options = new LilypondSnippetOptions();
        List<string> all = new List<string>();
        List<string> unrecognized = new List<string>();
        foreach (string entry in Split(rawOptions))
        {
            all.Add(entry);
            if (!Apply(options, entry))
            {
                unrecognized.Add(entry);
            }
        }
        options.All = all;
        options.Unrecognized = unrecognized;
        return options;
    }

    private static IEnumerable<string> Split(string rawOptions)
    {
        if (string.IsNullOrEmpty(rawOptions))
        {
            yield break;
        }
        int open = rawOptions.IndexOf('[');
        if (open < 0)
        {
            yield break;
        }
        int close = rawOptions.IndexOf(']', open + 1);
        if (close < 0)
        {
            close = rawOptions.Length;
        }
        string inside = rawOptions.Substring(open + 1, close - open - 1);
        foreach (string part in inside.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private static bool Apply(LilypondSnippetOptions options, string entry)
    {
        string name = entry;
        string value = string.Empty;
        int equals = entry.IndexOf('=');
        if (equals >= 0)
        {
            name = entry.Substring(0, equals).Trim();
            value = entry.Substring(equals + 1).Trim();
        }
        switch (name.ToLowerInvariant())
        {
            case "quote":
                options.Quote = true;
                return true;
            case "verbatim":
                options.Verbatim = true;
                return true;
            case "inline":
                options.Inline = true;
                return true;
            case "notime":
                options.NoTime = true;
                return true;
            case "texidoc":
                options.TexiDoc = true;
                return true;
            case "doctitle":
                options.DocTitle = true;
                return true;
            case "noindent":
                options.NoIndent = true;
                return true;
            case "ragged-right":
                options.RaggedRight = true;
                return true;
            case "noragged-right":
                options.RaggedRight = false;
                return true;
            case "fragment":
                options.Fragment = true;
                return true;
            case "nofragment":
                options.Fragment = false;
                return true;
            case "relative":
                //Bare 'relative' is relative=1, which is what lilypond-book takes it for.
                if (value.Length == 0)
                {
                    options.Relative = 1;
                    return true;
                }
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int octave))
                {
                    options.Relative = octave;
                    return true;
                }
                return false;
            case "staffsize":
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double size))
                {
                    options.StaffSize = size;
                    return true;
                }
                return false;
            case "line-width":
                return AssignDimension(value, v => options.LineWidth = v);
            case "indent":
                return AssignDimension(value, v => options.Indent = v);
            case "paper-width":
                return AssignDimension(value, v => options.PaperWidth = v);
            case "paper-height":
                return AssignDimension(value, v => options.PaperHeight = v);
            case "papersize":
                if (value.Length == 0)
                {
                    return false;
                }
                options.PaperSize = value;
                return true;
            default:
                return false;
        }
    }

    private static bool AssignDimension(string value, Action<string> assign)
    {
        //A dimension with nothing after the '=' says nothing; leaving the property empty and
        //reporting the option is more use to a reader than recording a blank.
        if (value.Length == 0)
        {
            return false;
        }
        assign(value);
        return true;
    }
}
