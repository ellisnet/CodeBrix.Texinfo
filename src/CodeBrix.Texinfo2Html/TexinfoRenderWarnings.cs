using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Diagnostics;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// Everything the library could not do exactly as the source asked, gathered as text. A Texinfo
/// document that has a problem in it still renders: the construct degrades to the nearest thing
/// that can be shown and a message lands here, so nothing is lost silently and nothing throws.
/// </summary>
public sealed class TexinfoRenderWarnings
{
    private readonly List<string> _messages = new List<string>();

    internal TexinfoRenderWarnings(TexinfoWarningCollection warnings)
    {
        if (warnings == null)
        {
            return;
        }
        foreach (TexinfoWarning warning in warnings)
        {
            _messages.Add(warning.ToString());
        }
    }

    /// <summary>The messages, in the order the run produced them.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>How many messages there are.</summary>
    public int Count => _messages.Count;
}
