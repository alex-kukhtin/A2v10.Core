// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

using A2v10.Data.Core.Extensions.Dynamic;

namespace A2v10.Metadata;

internal class ReportGrouping
{
    internal List<ReportItemMetadata> Filters { get; }
    internal List<ReportItemMetadata> Grouping { get; }
    internal List<ReportItemMetadata> Data { get; }

    private readonly TableMetadata _report;
    private readonly ExpandoObject _prms;
    public ReportGrouping(TableMetadata report, ExpandoObject prms)
    {
        _prms = prms;
        _report = report;   
        Filters = [.. report.TypedReportItems(ReportItemKind.Filter)];

        var gparam = GroupParams;
        if (String.IsNullOrEmpty(gparam))
            Grouping = [.. report.TypedReportItems(ReportItemKind.Grouping).Where(c => c.Checked)];
        else
        {
            var gitems = report.TypedReportItems(ReportItemKind.Grouping);
            Grouping = [..gparam.Split('!').Join(gitems, x => x, y => y.Column, (x, y) => y)];
        }
        var dparam = DataParams;
        if (String.IsNullOrEmpty(dparam))
            Data = [.. report.TypedReportItems(ReportItemKind.Data).Where(c => c.Checked)];
        else
        {
            var ditems = report.TypedReportItems(ReportItemKind.Data);
            Data = [.. dparam.Split('!').Join(ditems, x => x, y => y.Column, (x, y) => y)];
        }
    }

    public String? GroupParams => _prms.Get<String>("Group");
    public String? DataParams => _prms.Get<String>("Data");
    internal String SelectFields(String alias)
        => String.Join(", ", Grouping.Select(c => $"{alias}.[{c.Column}]"));

    internal String SimpleFields()
        => String.Join(", ", Grouping.Select(c => $"[{c.Column}]"));

    internal String FieldsWithGrouping()
        => String.Join(", ", Grouping.Select(c => $"[{c.Column}], {c.Column}Grp = grouping([{c.Column}])"));

    internal String ReferenceWithGrouping()
        => String.Join(", ", Grouping.Select((c, ix) => $"[{c.Column}.Id!T{c.Column}!Id] = T.{c.Column}, [{c.Column}.Name!T{c.Column}!Name] = isnull(r{ix + 1}.[Name], N'@[{c.Column}.NoData]'), [{c.Column}!!GroupMarker] = {c.Column}Grp"));

    internal String ReferenceJoins()
        => String.Join(" ", Grouping.Select((c, ix) => $"left join {c.RefSchema}.[{c.RefTable}] r{ix + 1} on T.[{c.Column}] = r{ix + 1}.[Id]"));

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
            [In{c.Column}] = sum(case when {alias}.Date > @From and InOut = 1 then {alias}.[{c.Column}] else 0 end),
            [Out{c.Column}] = sum(case when {alias}.Date > @From and InOut = -1 then {alias}.[{c.Column}] else 0 end)
            """)
        );

    internal String SqlWhereClause(String alias)
    {
        var where = String.Join(" and ", Filters.Select(c => $"(@{c.Column} is null or {alias}.[{c.Column}] = @{c.Column})"));
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

    internal String RepInfoSql()
    {
        var otherItems = _report.TypedReportItems(ReportItemKind.Grouping).Except(Grouping);
        var otherData = _report.TypedReportItems(ReportItemKind.Data).Except(Data);

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
