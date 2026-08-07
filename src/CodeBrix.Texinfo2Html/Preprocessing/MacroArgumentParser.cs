using System.Collections.Generic;
using System.Text;

namespace CodeBrix.Texinfo2Html.Preprocessing;

/// <summary>
/// The pure text rules of Texinfo macro invocation and expansion: splitting brace-form argument
/// lists, backslash quoting, and parameter substitution in macro bodies. Kept free of token and
/// stream concerns so the rules are directly unit-testable.
/// </summary>
internal static class MacroArgumentParser
{
    /// <summary>
    /// Splits the raw text of a brace-form invocation into arguments. For a macro with more
    /// than one parameter the text is split at top-level commas; commas inside nested brace
    /// groups do not split, <c>@</c>-pairs are opaque, and <c>\,</c> <c>\{</c> <c>\}</c>
    /// <c>\\</c> insert the literal character. A single-parameter macro receives the entire
    /// text (commas are not special), with the same backslash quoting applied. Each argument
    /// is trimmed of surrounding whitespace.
    /// </summary>
    /// <param name="text">The raw text between the invocation braces.</param>
    /// <param name="parameterCount">The number of formal parameters of the macro.</param>
    public static List<string> SplitBraceArguments(string text, int parameterCount)
    {
        List<string> arguments = new List<string>();
        if (parameterCount <= 1)
        {
            arguments.Add(UnescapeQuotedCharacters(text).Trim());
            return arguments;
        }
        SplitAtTopLevelCommas(text, arguments, unescape: true);
        return arguments;
    }

    /// <summary>
    /// Splits the rest-of-line text of a line-form invocation into arguments. Line-form
    /// arguments are taken verbatim - no backslash unescaping - which matches how real-world
    /// documents index literal backslash commands (for example LilyPond's
    /// <c>@funindex \relative</c>). A single-parameter macro receives the whole line.
    /// </summary>
    /// <param name="text">The rest-of-line text after the macro name.</param>
    /// <param name="parameterCount">The number of formal parameters of the macro.</param>
    public static List<string> SplitLineArguments(string text, int parameterCount)
    {
        List<string> arguments = new List<string>();
        if (parameterCount <= 1)
        {
            arguments.Add(text.Trim());
            return arguments;
        }
        SplitAtTopLevelCommas(text, arguments, unescape: false);
        return arguments;
    }

    /// <summary>
    /// Substitutes arguments into a macro body. Within a body, <c>\name\</c> is replaced by the
    /// corresponding argument (verbatim - argument text is never rescanned for parameters),
    /// <c>\\</c> yields a literal backslash, and any other backslash sequence is kept as
    /// literal text.
    /// </summary>
    /// <param name="body">The raw macro body text.</param>
    /// <param name="parameters">The macro's formal parameter names.</param>
    /// <param name="arguments">The argument values; missing trailing arguments become empty.</param>
    /// <param name="unknownParameterReferences">
    /// Receives each parameter-like name that appeared between backslashes but is not a formal
    /// parameter, so the caller can warn about probable typos.
    /// </param>
    public static string SubstituteBody(string body, IReadOnlyList<string> parameters,
        IReadOnlyList<string> arguments, List<string> unknownParameterReferences)
    {
        StringBuilder result = new StringBuilder(body.Length);
        int index = 0;
        while (index < body.Length)
        {
            char c = body[index];
            if (c != '\\')
            {
                result.Append(c);
                index++;
                continue;
            }
            int close = body.IndexOf('\\', index + 1);
            if (close < 0)
            {
                result.Append(body, index, body.Length - index);
                break;
            }
            string between = body.Substring(index + 1, close - index - 1);
            if (between.Length == 0)
            {
                result.Append('\\');
                index = close + 1;
                continue;
            }
            int parameterIndex = IndexOf(parameters, between);
            if (parameterIndex >= 0)
            {
                if (parameterIndex < arguments.Count)
                {
                    result.Append(arguments[parameterIndex]);
                }
                index = close + 1;
                continue;
            }
            if (parameters.Count > 0 && LooksLikeParameterName(between))
            {
                unknownParameterReferences.Add(between);
            }
            // Not a parameter reference: keep the text and resume at the closing backslash,
            // which may itself open the next reference.
            result.Append('\\').Append(between);
            index = close;
        }
        return result.ToString();
    }

    private static void SplitAtTopLevelCommas(string text, List<string> arguments, bool unescape)
    {
        StringBuilder current = new StringBuilder();
        int depth = 0;
        int index = 0;
        while (index < text.Length)
        {
            char c = text[index];
            if (unescape && c == '\\' && index + 1 < text.Length)
            {
                char next = text[index + 1];
                if (next == ',' || next == '{' || next == '}' || next == '\\')
                {
                    current.Append(next);
                    index += 2;
                    continue;
                }
                current.Append(c);
                index++;
                continue;
            }
            if (c == '@' && index + 1 < text.Length)
            {
                current.Append(c).Append(text[index + 1]);
                index += 2;
                continue;
            }
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (c == ',' && depth == 0)
            {
                arguments.Add(current.ToString().Trim());
                current.Clear();
                index++;
                continue;
            }
            current.Append(c);
            index++;
        }
        arguments.Add(current.ToString().Trim());
    }

    private static string UnescapeQuotedCharacters(string text)
    {
        if (text.IndexOf('\\') < 0)
        {
            return text;
        }
        StringBuilder result = new StringBuilder(text.Length);
        int index = 0;
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '\\' && index + 1 < text.Length)
            {
                char next = text[index + 1];
                if (next == ',' || next == '{' || next == '}' || next == '\\')
                {
                    result.Append(next);
                    index += 2;
                    continue;
                }
            }
            result.Append(c);
            index++;
        }
        return result.ToString();
    }

    private static int IndexOf(IReadOnlyList<string> parameters, string name)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (string.Equals(parameters[i], name, System.StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static bool LooksLikeParameterName(string text)
    {
        if (!char.IsLetter(text[0]))
        {
            return false;
        }
        foreach (char c in text)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }
        return true;
    }
}
