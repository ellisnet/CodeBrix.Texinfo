using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A <c>@float</c> environment: a figure, table or listing that carries a caption and a number,
/// and that cross references address by the label on its opening line.
/// </summary>
/// <remarks>
/// A float in a printed document is allowed to drift away from the text that mentions it, which is
/// why it is numbered and captioned at all. Nothing here moves it - the output subset has no
/// floating - but the numbering and the caption are what make the reference "see Figure 1.2"
/// meaningful, and those this does provide.
/// </remarks>
internal sealed class FloatNode : TexinfoNode
{
    /// <summary>Creates a float.</summary>
    /// <param name="typeName">The float's type, such as "Figure"; empty when the source gave none.</param>
    /// <param name="label">The cross-reference label; empty when the source gave none.</param>
    /// <param name="blocks">The float's content.</param>
    /// <param name="position">Where the environment started in the source.</param>
    public FloatNode(string typeName, string label, IReadOnlyList<TexinfoNode> blocks,
        SourcePosition position) : base(position)
    {
        TypeName = typeName ?? string.Empty;
        Label = label ?? string.Empty;
        Blocks = blocks ?? new List<TexinfoNode>();
        Caption = new List<TexinfoNode>();
        ShortCaption = new List<TexinfoNode>();
    }

    /// <summary>The float's type, such as "Figure" or "Table"; empty when the source gave none.</summary>
    public string TypeName { get; }

    /// <summary>The label a cross reference addresses the float by; empty when it has none.</summary>
    public string Label { get; }

    /// <summary>The float's content.</summary>
    public IReadOnlyList<TexinfoNode> Blocks { get; }

    /// <summary>The <c>@caption</c> text, printed under the float; empty when it has none.</summary>
    public IReadOnlyList<TexinfoNode> Caption { get; set; }

    /// <summary>
    /// The <c>@shortcaption</c> text, used in a list of floats in place of the full caption; empty
    /// when the float has none.
    /// </summary>
    public IReadOnlyList<TexinfoNode> ShortCaption { get; set; }

    /// <summary>
    /// The float's number within its type, such as "1.2", filled in by the semantic pass. Empty
    /// for a float whose type the source never named, which is therefore not countable.
    /// </summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// How a cross reference to this float reads - its type and number, as in "Figure 1.2". Empty
    /// when the float has no type to name.
    /// </summary>
    public string ReferenceText
        => TypeName.Length == 0
            ? string.Empty
            : Number.Length == 0 ? TypeName : TypeName + " " + Number;

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (TexinfoNode node in Blocks)
            {
                yield return node;
            }
            foreach (TexinfoNode node in Caption)
            {
                yield return node;
            }
            foreach (TexinfoNode node in ShortCaption)
            {
                yield return node;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@float {TypeName} {Number}".TrimEnd();
}
