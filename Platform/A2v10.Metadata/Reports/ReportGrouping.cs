// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

using A2v10.Data.Core.Extensions.Dynamic;

namespace A2v10.Metadata;

public enum ReportItemKind
{
    G,
    F,
    D,
    Grouping = G,
    Filter = F,
    Data = D
}
public record ReportItemMetadata
{
    public ReportItemKind Kind { get; init; }
    public String Column { get; init; } = default!;
    public ColumnType DataType { get; init; } = default!;
    // the referenced table: a report shows its Presentation, read from RefSourceName, as everywhere else
    public TableMetadata? Target { get; init; }
    public String? Endpoint { get; init; }
    public Boolean Checked { get; init; }
    public Int32 Order { get; init; }
    public String? Label { get; init; }

    // the header of the item, on the page and in the picks the query sends back (RepInfoSql)
    internal String LocalizeLabel() => Label.Localize() ?? $"@[{Column}]";
}

internal class ReportGrouping
{
    internal List<ReportItemMetadata> Filters { get; }
    internal List<ReportItemMetadata> Grouping { get; }
    internal List<ReportItemMetadata> Data { get; }

    private readonly ReportMetadata _report;
    private readonly TableMetadata _surface;
    private readonly ExpandoObject _prms;

    public ReportGrouping(ReportMetadata report, TableMetadata surface, ExpandoObject prms)
    {
        _prms = prms;
        _report = report;   
        _surface = surface;
        Filters = [.. TypedReportItems(ReportItemKind.Filter)];

        var gparam = GroupParams;
        if (String.IsNullOrEmpty(gparam))
            Grouping = [.. TypedReportItems(ReportItemKind.Grouping).Where(c => c.Checked)];
        else
        {
            var gitems = TypedReportItems(ReportItemKind.Grouping);
            Grouping = [..gparam.Split('!').Join(gitems, x => x, y => y.Column, (x, y) => y)];
        }
        var dparam = DataParams;
        if (String.IsNullOrEmpty(dparam))
            Data = [.. TypedReportItems(ReportItemKind.Data).Where(c => c.Checked)];
        else
        {
            var ditems = TypedReportItems(ReportItemKind.Data);
            Data = [.. dparam.Split('!').Join(ditems, x => x, y => y.Column, (x, y) => y)];
        }
    }

    // the browser's text: read here, resolved against the declared items, and nowhere else
    private String? GroupParams => _prms.Get<String>("Group");
    private String? DataParams => _prms.Get<String>("Data");

    // the picked groups as resolved - declared names only, so the echo of '?Group=' can be SQL text
    internal String GroupColumns()
        => String.Join("!", Grouping.Select(c => c.Column));

    internal String SelectFields(String alias)
        => String.Join(", ", Grouping.Select(c => $"{alias}.[{c.Column}]"));

    internal String SimpleFields()
        => String.Join(", ", Grouping.Select(c => $"[{c.Column}]"));

    internal String FieldsWithGrouping()
        => String.Join(", ", Grouping.Select(c => $"[{c.Column}], {c.Column}Grp = grouping([{c.Column}])"));

    /* A group and a filter show a reference the way the platform shows one: the target's Presentation, read
     * from RefSourceName - a document's reference view. Read as [Name] of the table, a document (no Name)
     * failed the report and a catalog with another presentation showed what the index does not.
     */
    internal String ReferenceWithGrouping()
        => String.Join(", ", Grouping.Select((c, ix) => $"[{c.Column}.Id!T{c.Column}!Id] = T.{c.Column}, [{c.Column}.Name!T{c.Column}!Name] = isnull(r{ix + 1}.[{c.Target!.Presentation}], N'@[{c.Column}.NoData]'), [{c.Column}!!GroupMarker] = {c.Column}Grp"));

    internal String ReferenceJoins()
        => String.Join(" ", Grouping.Select((c, ix) => $"left join {c.Target!.RefSourceName} r{ix + 1} on T.[{c.Column}] = r{ix + 1}.[Id]"));

    /* The object each filter's RefId points at. One for every report type - it was written twice, in the
     * turnover and the ledger builder. The type stays T{Column}, one per filter, as the page binds it.
     */
    internal String FilterMapsSql()
        => String.Join("\n", Filters.Select(f => $"""
            select [!T{f.Column}!Map] = null, [Id!!Id] = a.[Id], [Name!!Name] = a.[{f.Target!.Presentation}]{SqlBuilder.RefFields(f.Target, "a")}
            from {f.Target.RefSourceName} a
            where a.[Id] = {SqlExtensions.ColumnParam(f.Column)};
            """));

    internal String ReferenceOrderByGrp()
        => String.Join(", ", Grouping.Select(c => $"{c.Column}Grp desc"));

    internal String SimpleDataFields()
        => String.Join(", ", Data.Select(c => $"[Start{c.Column}], [In{c.Column}], [Out{c.Column}]"));
    internal String FullDataFields()
        => String.Join(", ", Data.Select(c => $"[Start{c.Column}], [In{c.Column}], [Out{c.Column}], End{c.Column} = [Start{c.Column}] + [In{c.Column}] - [Out{c.Column}]"));

    internal String InsertIntoDataFields(String alias)
        => String.Join(", ", Data.Select(c =>
            $"""                        
            [Start{c.Column}] = sum(case when {alias}.[Date] < @From then {alias}.[{c.Column}]* j.[InOut] else 0 end),
            [In{c.Column}] = sum(case when {alias}.Date >= @From and InOut = 1 then {alias}.[{c.Column}] else 0 end),
            [Out{c.Column}] = sum(case when {alias}.Date >= @From and InOut = -1 then {alias}.[{c.Column}] else 0 end)
            """)
        );

    internal String SqlWhereClause(String alias)
    {
        var where = String.Join(" and ", Filters.Select(c =>
            $"({SqlExtensions.ColumnParam(c.Column)} is null or {alias}.[{c.Column}] = {SqlExtensions.ColumnParam(c.Column)})"));
        if (String.IsNullOrEmpty(where))
            return String.Empty;
        return $" and {where}";
    }

    internal String AggregateDataFields()
        => String.Join(", ", Data.Select(c =>
            $"""                        
            [Start{c.Column}] = sum([Start{c.Column}]), [In{c.Column}] = sum([In{c.Column}]), [Out{c.Column}] = sum([Out{c.Column}])           
            """)
        );


    internal IEnumerable<ReportItemMetadata> TypedReportItems(ReportItemKind kind)
    {
        ReportItemMetadata CreateReportItemMetadata(String name)
        {
            // the baseline counts: a ledger's Sum, Acc, CorrAcc are columns of the surface too
            var column = _surface.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"Column '{name}' not found in surface '{_surface.Path}'");
            Boolean isChecked = false;
            if (kind == ReportItemKind.Grouping)
                isChecked = _report.Defaults.Groups.Contains(name);
            else if (kind == ReportItemKind.Data)
                isChecked = _report.Defaults.Data.Contains(name);
            return new ReportItemMetadata()
            {
                Kind = kind,
                Column = name,
                DataType = column.Type,
                Target = column.IsRef ? column.RefTableCheck.Storage : null,
                Endpoint = column.IsRef ? column.RefTableCheck.Path : null,
                Checked = isChecked
            };
        }

        return kind switch
        {
            ReportItemKind.Grouping => _report.Groups.Select(CreateReportItemMetadata),
            ReportItemKind.Data => _report.Data.Select(CreateReportItemMetadata),
            ReportItemKind.Filter => _report.Filters.Select(CreateReportItemMetadata),
            _ => throw new InvalidOperationException($"Unknown kind {kind}")
        };
    }

    internal String RepInfoSql()
    {
        var otherItems = TypedReportItems(ReportItemKind.Grouping).Except(Grouping);
        var otherData = TypedReportItems(ReportItemKind.Data).Except(Data);

        String isCheckedGroup(ReportItemMetadata item)
            => Grouping.Any(c => c.Column == item.Column) ? "1" : "0";

        String isCheckedData(ReportItemMetadata item)
            => Data.Any(c => c.Column == item.Column) ? "1" : "0";

        var items = Grouping.Union(otherItems).Select((c, ix) => $"(N'G', N'{c.Column}', N'{c.LocalizeLabel()}', {ix + 1}, {isCheckedGroup(c)})");
        var dataItems = Data.Union(otherData).Select((c, ix) => $"(N'D', N'{c.Column}', N'{c.LocalizeLabel()}', {ix + 1}, {isCheckedData(c)})");

        return $"""
        declare @grptmp table (Kind nchar(1), Id nvarchar(255), Label nvarchar(255), [Order] int, Checked bit);
        insert into @grptmp (Kind, Id, Label, [Order], [Checked]) values
        {String.Join(",\n", items)};
        insert into @grptmp (Kind, Id, Label, [Order], [Checked]) values
        {String.Join(",\n", dataItems)};
        
        select [GroupingInfo!TGInfo!Array] = null, Id, [Label], [Order!!RowNumber] = [Order], [Checked] 
        from @grptmp where Kind = N'G' order by [Order];

        select [DataInfo!TGInfo!Array] = null, Id, [Label], [Order!!RowNumber] = [Order], [Checked] 
        from @grptmp where Kind = N'D' order by [Order];
        """;
    }
}
