using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CodeBrix.Texinfo2Html.Semantics;

/// <summary>
/// Hands out unique HTML identifiers derived from Texinfo node and anchor names. Texinfo names are
/// free text - they carry spaces, punctuation and, in translated manuals, non-Latin letters - so
/// they are folded into something that survives an <c>id</c> attribute and a <c>#name</c> link
/// while staying recognizable to anyone reading the generated markup.
/// </summary>
/// <remarks>
/// Letters and digits of any script are kept, because both the HTML parser and the PDF bookmark
/// table handle them; everything else collapses to a single hyphen. A name that folds away to
/// nothing falls back to a numbered identifier, which keeps Cyrillic-only or punctuation-only
/// names from all colliding on the same empty slug.
/// </remarks>
internal sealed class ElementIdAllocator
{
    private readonly HashSet<string> _used = new HashSet<string>(System.StringComparer.Ordinal);
    private int _fallbackCounter;

    /// <summary>
    /// Returns an identifier that has not been handed out before, derived from the given text.
    /// </summary>
    /// <param name="preferredText">The Texinfo name, or any text, to derive the identifier from.</param>
    public string Allocate(string preferredText)
    {
        string slug = Slug(preferredText);
        if (slug.Length == 0)
        {
            _fallbackCounter++;
            slug = "id-" + _fallbackCounter.ToString(CultureInfo.InvariantCulture);
        }
        if (_used.Add(slug))
        {
            return slug;
        }
        for (int suffix = 2; ; suffix++)
        {
            string candidate = slug + "-" + suffix.ToString(CultureInfo.InvariantCulture);
            if (_used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Slug(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        StringBuilder builder = new StringBuilder(text.Length);
        bool pendingSeparator = false;
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                pendingSeparator = false;
                builder.Append(c);
                continue;
            }
            pendingSeparator = true;
        }
        //An identifier must not open with a digit: a CSS selector cannot address one, and the
        //document's own stylesheet is the first thing that would need to.
        if (builder.Length > 0 && char.IsDigit(builder[0]))
        {
            builder.Insert(0, "n-");
        }
        return builder.ToString();
    }
}
