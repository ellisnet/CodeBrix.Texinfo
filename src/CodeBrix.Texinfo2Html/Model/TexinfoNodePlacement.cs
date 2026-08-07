using System;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// Where a node may legally appear in the document tree. Several Texinfo commands -
/// <c>@anchor</c>, <c>@cindex</c>, <c>@image</c> and the lilypond-book music environments among
/// them - are valid both standing alone between paragraphs and in the middle of one, so the model
/// keeps a single node hierarchy and records each node's legal placement rather than splitting
/// blocks and inlines into two separate trees.
/// </summary>
[Flags]
internal enum TexinfoNodePlacement
{
    /// <summary>The node stands on its own, between paragraphs.</summary>
    Block = 1,

    /// <summary>The node appears within a paragraph's inline content.</summary>
    Inline = 2,

    /// <summary>The node is valid in either position.</summary>
    Both = Block | Inline
}
