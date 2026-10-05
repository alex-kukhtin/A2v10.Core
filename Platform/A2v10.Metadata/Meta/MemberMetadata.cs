// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

// what a name in 'fields' resolves to - see CLAUDE.md, "Members"
public enum MemberKind
{
    Column,
    Tags,
    // a '$' name of the declaration's 'properties': a value the client holds, with no column
    Property
}

/* One member of the record a form node may show. 'Column' for the kind a column contributes and
 * for no other, so a consumer that only ever draws columns says ColumnCheck and is done. A property
 * over a column rides on the column's member: the control is still the column's, and the property
 * only says whether it can be typed into.
 */
public sealed record MemberDescriptor(MemberKind Kind, String Name, TableColumn? Column = null, PropertyMetadata? Property = null)
{
    internal TableColumn ColumnCheck => Column
        ?? throw new InvalidOperationException($"Member '{Name}' ({Kind}) has no column");

    internal ColumnType Type => Column?.Type ?? Property?.Type
        ?? throw new InvalidOperationException($"Member '{Name}' ({Kind}) has no type");

    internal String ModelName => Column?.ModelName ?? Name;

    internal String Header => Column?.Header ?? $"@[{Name.TrimStart('$')}]";

    // what was typed into it would not be kept: a getter without a setter, a column the database computes
    internal Boolean IsShownOnly => Property is { Set: null } || Column?.HasSqlAs == true;

    internal static MemberDescriptor Of(TableColumn column) =>
        new(MemberKind.Column, column.Name, column);
}

internal static class MemberMetadata
{
    /* The candidates for one form, and the only place that says which form sees what. A predicate
     * on TableColumn cannot express a member that is not a column, which is why these are lists
     * and not filters - see CLAUDE.md, "Members".
     */
    public static List<MemberDescriptor> IndexMembers(this TableMetadata table) =>
        [.. table.AllColumns(TableColumnPredicates.IsIndexColumn).Select(MemberDescriptor.Of)];

    public static List<MemberDescriptor> EditMembers(this TableMetadata table)
    {
        List<MemberDescriptor> members =
            [.. table.AllColumns(TableColumnPredicates.IsEditColumn).Select(MemberDescriptor.Of)];
        if (table.HasTags)
            members.Add(new MemberDescriptor(MemberKind.Tags, Constants.FieldNames.Tags));
        return members;
    }

    // the rows of a collection: columns and nothing else, the discriminator excluded
    public static List<MemberDescriptor> RowMembers(this TableMetadata table) =>
        [.. table.AllColumns(c => c.Type != ColumnType.RowKind).Select(MemberDescriptor.Of)];

    /* The declared properties added to the candidates of a form. Only the edit form is handed them:
     * the edit template is where properties are generated, and a list or a picker has none.
     */
    public static List<MemberDescriptor> WithProperties(this List<MemberDescriptor> members,
        IReadOnlyDictionary<String, PropertyMetadata> properties)
    {
        if (properties.Count == 0)
            return members;
        return [
            .. members.Select(m => properties.TryGetValue(m.Name, out var p) ? m with { Property = p } : m),
            .. properties.Where(kp => PropertyMetadata.IsOwnName(kp.Key))
                .Select(kp => new MemberDescriptor(MemberKind.Property, kp.Key, Property: kp.Value))
        ];
    }

    /* A journal's rows seen from the document that posted them: its index columns minus the four
     * the document FIXES. Every row of the dialog shares one document, one date, one operation, so
     * those columns carry no information here and the Document one would render as a link back to
     * the page you are standing on.
     *
     * By ColumnType, not by the post mapping. 'Filled from the header' is not the same question:
     * a journal's Agent comes from the header too, is equally constant for this document, and is
     * exactly what the dialog is read for.
     */
    public static List<MemberDescriptor> TransMembers(this TableMetadata journal)
    {
        static Boolean FixedByDocument(TableColumn col) =>
            col.Type is ColumnType.Document or ColumnType.DocumentType
                or ColumnType.Date or ColumnType.Operation;

        return [.. journal.AllColumns(c => TableColumnPredicates.IsIndexColumn(c) && !FixedByDocument(c))
            .Select(MemberDescriptor.Of)];
    }
}
