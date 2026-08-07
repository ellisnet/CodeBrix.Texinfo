using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// The base class of every node in a parsed Texinfo document tree. Nodes are immutable once the
/// parser has built them, apart from the few back-references that later passes fill in, and every
/// node knows where it came from so warnings raised long after parsing can still point at source.
/// </summary>
internal abstract class TexinfoNode
{
    /// <summary>Creates a node at the given source position.</summary>
    /// <param name="position">Where the construct that produced this node started.</param>
    protected TexinfoNode(SourcePosition position)
    {
        Position = position;
    }

    /// <summary>Where the construct that produced this node started in the source.</summary>
    public SourcePosition Position { get; }

    /// <summary>Where this kind of node may legally appear.</summary>
    public abstract TexinfoNodePlacement Placement { get; }

    /// <summary>
    /// This node's immediate children, in document order, across every child collection the node
    /// owns. Leaf nodes yield nothing.
    /// </summary>
    public virtual IEnumerable<TexinfoNode> ChildNodes
    {
        get { yield break; }
    }

    /// <summary>Every node beneath this one, depth first and in document order.</summary>
    public IEnumerable<TexinfoNode> DescendantNodes()
    {
        foreach (TexinfoNode child in ChildNodes)
        {
            yield return child;
            foreach (TexinfoNode descendant in child.DescendantNodes())
            {
                yield return descendant;
            }
        }
    }
}
