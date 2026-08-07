using System.Text;

namespace CodeBrix.Texinfo2Html.Emit;

/// <summary>
/// Wraps generated body markup in a complete HTML document, either pointing at a stylesheet file
/// beside it or carrying the stylesheet inside it.
/// </summary>
/// <remarks>
/// The linked form is the default because it is the friendlier one to edit: a consumer who wants a
/// different look changes one .css file and re-renders, without touching the markup. Html2Pdf
/// follows <c>link rel="stylesheet"</c> to local files, resolving them against the directory of the
/// HTML file it was given, so the pair renders with no extra configuration.
/// </remarks>
internal static class HtmlDocumentBuilder
{
    /// <summary>Builds a document that links to a stylesheet file beside it.</summary>
    /// <param name="bodyHtml">The generated body markup.</param>
    /// <param name="cssFileName">The stylesheet's file name, as written in the link.</param>
    /// <param name="title">The document title.</param>
    public static string Build(string bodyHtml, string cssFileName, string title)
    {
        StringBuilder builder = StartDocument(title);
        builder.Append("<link rel=\"stylesheet\" href=\"").Append(Escape(cssFileName)).Append("\">\n");
        return FinishDocument(builder, bodyHtml);
    }

    /// <summary>Builds a self-contained document with the stylesheet embedded in it.</summary>
    /// <param name="bodyHtml">The generated body markup.</param>
    /// <param name="css">The stylesheet text.</param>
    /// <param name="title">The document title.</param>
    public static string BuildSelfContained(string bodyHtml, string css, string title)
    {
        StringBuilder builder = StartDocument(title);
        builder.Append("<style>\n").Append(css).Append("\n</style>\n");
        return FinishDocument(builder, bodyHtml);
    }

    private static StringBuilder StartDocument(string title)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n");
        builder.Append("<title>").Append(Escape(title)).Append("</title>\n");
        return builder;
    }

    private static string FinishDocument(StringBuilder builder, string bodyHtml)
    {
        builder.Append("</head>\n<body>\n");
        builder.Append(bodyHtml);
        builder.Append("\n</body>\n</html>\n");
        return builder.ToString();
    }

    private static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        StringBuilder builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '"':
                    builder.Append("&quot;");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }
        return builder.ToString();
    }
}
