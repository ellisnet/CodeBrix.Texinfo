using System;
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
    /// Splits the rest-of-line text of a LINE MACRO invocation into arguments. These follow
    /// different rules from every other invocation form: arguments are separated by SPACES rather
    /// than commas, a pair of braces enclosing an argument is removed, an empty argument has to be
    /// written as <c>{}</c>, and the last argument takes the whole remainder of the line so that
    /// it may contain spaces without being braced. Arguments are taken verbatim - no backslash
    /// unescaping - as line-form arguments are everywhere else.
    /// </summary>
    /// <param name="text">The rest-of-line text after the macro name.</param>
    /// <param name="parameterCount">The number of formal parameters of the macro.</param>
    public static List<string> SplitLineMacroArguments(string text, int parameterCount)
    {
        List<string> arguments = new List<string>();
        if (parameterCount <= 0)
        {
            if (text.Trim().Length > 0)
            {
                arguments.Add(text.Trim());
            }
            return arguments;
        }
        int index = 0;
        SkipSpaces(text, ref index);
        while (arguments.Count < parameterCount - 1 && index < text.Length)
        {
            arguments.Add(ReadLineMacroArgument(text, ref index));
            SkipSpaces(text, ref index);
        }

        //The final argument is the rest of the line, which is what lets it hold spaces unbraced.
        //It is still unwrapped when the whole of it is one brace group, so that '{}' is empty and
        //'{a b}' is 'a b'.
        string remainder = text.Substring(index).Trim();
        int scan = 0;
        if (remainder.StartsWith("{", StringComparison.Ordinal))
        {
            string unwrapped = ReadLineMacroArgument(remainder, ref scan);
            arguments.Add(scan >= remainder.Length ? unwrapped : remainder);
        }
        else
        {
            arguments.Add(remainder);
        }
        return arguments;
    }

    /// <summary>
    /// Splits the braced argument list of a built-in command at its top-level commas, keeping at
    /// most <paramref name="maximumArguments"/> of them: a comma inside the last argument is text
    /// rather than a separator, which is what lets an inline conditional carry a sentence.
    /// Backslashes are ordinary characters here - that quoting belongs to macro invocation, not to
    /// the built-in commands.
    /// </summary>
    /// <param name="text">The raw text between the command's braces.</param>
    /// <param name="maximumArguments">The most arguments to produce.</param>
    public static List<string> SplitCommandArguments(string text, int maximumArguments)
    {
        List<string> arguments = new List<string>();
        StringBuilder current = new StringBuilder();
        int depth = 0;
        int index = 0;
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '@' && index + 1 < text.Length)
            {
                //An @-pair is opaque, which is how @comma{} writes a comma that does not split.
                current.Append(c).Append(text[index + 1]);
                index += 2;
                continue;
            }
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && depth > 0)
            {
                depth--;
            }
            else if (c == ',' && depth == 0 && arguments.Count < maximumArguments - 1)
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

    /// <summary>
    /// Reads one space-separated line-macro argument starting at <paramref name="index"/>, which
    /// must be at a non-space character, and leaves the index just past it. A leading brace makes
    /// the argument run to the matching close brace, and those outer braces are dropped.
    /// </summary>
    private static string ReadLineMacroArgument(string text, ref int index)
    {
        if (text[index] == '{')
        {
            int depth = 0;
            int start = index;
            while (index < text.Length)
            {
                char c = text[index];
                if (c == '@' && index + 1 < text.Length)
                {
                    //An @-pair is opaque, which is how @{ and the '@' ending a continued line
                    //stay inside the argument instead of closing or splitting it.
                    index += 2;
                    continue;
                }
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string inner = text.Substring(start + 1, index - start - 1);
                        index++;
                        return inner;
                    }
                }
                index++;
            }
            return text.Substring(start + 1);
        }

        int begin = index;
        int braceDepth = 0;
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '@' && index + 1 < text.Length)
            {
                index += 2;
                continue;
            }
            if (c == '{')
            {
                braceDepth++;
            }
            else if (c == '}')
            {
                if (braceDepth > 0)
                {
                    braceDepth--;
                }
            }
            else if (braceDepth == 0 && char.IsWhiteSpace(c))
            {
                break;
            }
            index++;
        }
        return text.Substring(begin, index - begin);
    }

    private static void SkipSpaces(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
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
            if (string.Equals(parameters[i], name, StringComparison.Ordinal))
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
