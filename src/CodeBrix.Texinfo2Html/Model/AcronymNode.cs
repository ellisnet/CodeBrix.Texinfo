using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// An <c>@acronym</c> or <c>@abbr</c>: a short form, and optionally the words it stands for. The
/// two commands differ only in how the short form is set, so they share one node and are told
/// apart by <see cref="IsAcronym"/>.
/// </summary>
internal sealed class AcronymNode : TexinfoNode
{
    /// <summary>Creates an acronym or abbreviation.</summary>
    /// <param name="isAcronym">True for <c>@acronym</c>, false for <c>@abbr</c>.</param>
    /// <param name="shortForm">The acronym or abbreviation itself.</param>
    /// <param name="meaning">The words it stands for; empty when the source gave none.</param>
    /// <param name="position">Where the command started in the source.</param>
    public AcronymNode(bool isAcronym, IReadOnlyList<TexinfoNode> shortForm,
        IReadOnlyList<TexinfoNode> meaning, SourcePosition position) : base(position)
    {
        IsAcronym = isAcronym;
        ShortForm = shortForm ?? new List<TexinfoNode>();
        Meaning = meaning ?? new List<TexinfoNode>();
    }

    /// <summary>True for <c>@acronym</c>, which is set in small capitals; false for <c>@abbr</c>.</summary>
    public bool IsAcronym { get; }

    /// <summary>The acronym or abbreviation itself.</summary>
    public IReadOnlyList<TexinfoNode> ShortForm { get; }

    /// <summary>The words the short form stands for; empty when the source gave none.</summary>
    public IReadOnlyList<TexinfoNode> Meaning { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Inline;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (TexinfoNode node in ShortForm)
            {
                yield return node;
            }
            foreach (TexinfoNode node in Meaning)
            {
                yield return node;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => IsAcronym ? "@acronym" : "@abbr";
}
