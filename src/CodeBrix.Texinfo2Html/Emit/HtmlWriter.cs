using System.Text;

namespace CodeBrix.Texinfo2Html.Emit;

/// <summary>
/// Builds the generated markup. It indents block elements so the output is readable, and stops
/// doing so inside preformatted content, where every character between the tags is part of the
/// document. Text and attribute values are escaped on the way in, so no caller has to remember to.
/// </summary>
internal sealed class HtmlWriter
{
    private readonly StringBuilder _builder = new StringBuilder();
    private int _depth;
    private bool _preformatted;

    /// <summary>The markup written so far.</summary>
    public override string ToString() => _builder.ToString();

    /// <summary>Opens the start tag of a block element, on a line of its own.</summary>
    /// <param name="tagName">The element name.</param>
    public void BeginBlock(string tagName)
    {
        NewLine();
        _depth++;
        _builder.Append('<').Append(tagName);
    }

    /// <summary>Opens the start tag of an inline element, in the current line.</summary>
    /// <param name="tagName">The element name.</param>
    public void BeginInline(string tagName) => _builder.Append('<').Append(tagName);

    /// <summary>Writes an attribute into the start tag being built; empty values are skipped.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value, escaped here.</param>
    public void Attribute(string name, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }
        _builder.Append(' ').Append(name).Append("=\"");
        AppendEscaped(value, inAttribute: true);
        _builder.Append('"');
    }

    /// <summary>Closes the start tag being built.</summary>
    public void CloseStartTag() => _builder.Append('>');

    /// <summary>Closes a block element, on a line of its own.</summary>
    /// <param name="tagName">The element name.</param>
    public void EndBlock(string tagName)
    {
        _depth--;
        NewLine();
        _builder.Append("</").Append(tagName).Append('>');
    }

    /// <summary>Closes an inline element, in the current line.</summary>
    /// <param name="tagName">The element name.</param>
    public void EndInline(string tagName) => _builder.Append("</").Append(tagName).Append('>');

    /// <summary>
    /// Writes a void block element - one with no closing tag, such as <c>hr</c> or a standalone
    /// <c>img</c> - and leaves its start tag open for attributes.
    /// </summary>
    /// <param name="tagName">The element name.</param>
    public void BeginVoidBlock(string tagName)
    {
        NewLine();
        _builder.Append('<').Append(tagName);
    }

    /// <summary>Writes escaped text.</summary>
    /// <param name="text">The text to write.</param>
    public void Text(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            AppendEscaped(text, inAttribute: false);
        }
    }

    /// <summary>Writes markup that is already escaped and must pass through untouched.</summary>
    /// <param name="markup">The markup to write.</param>
    public void Raw(string markup) => _builder.Append(markup);

    /// <summary>Stops indentation, for the content of a preformatted element.</summary>
    public void BeginPreformatted() => _preformatted = true;

    /// <summary>Resumes indentation after preformatted content.</summary>
    public void EndPreformatted() => _preformatted = false;

    private void NewLine()
    {
        if (_preformatted)
        {
            return;
        }
        if (_builder.Length > 0)
        {
            _builder.Append('\n');
        }
        //A start tag is written before its element is counted and an end tag after its element is
        //uncounted, so in both cases the current depth is the element's own indent level.
        _builder.Append(' ', _depth * 2);
    }

    private void AppendEscaped(string text, bool inAttribute)
    {
        foreach (char c in text)
        {
            switch (c)
            {
                case '&':
                    _builder.Append("&amp;");
                    break;
                case '<':
                    _builder.Append("&lt;");
                    break;
                case '>':
                    _builder.Append("&gt;");
                    break;
                case '"':
                    _builder.Append(inAttribute ? "&quot;" : "\"");
                    break;
                default:
                    _builder.Append(c);
                    break;
            }
        }
    }
}
