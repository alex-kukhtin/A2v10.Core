// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class TurnoverReportBuilder(IServiceProvider serviceProvider, TableMetadata report, TableMetadata source) 
    : BaseReportBuilder(serviceProvider, report, source)
{
    public override async Task<IDataModel> LoadReportModelAsync(IModelView view, ExpandoObject prms)
    {
        _grouping = new ReportGrouping(_report, prms);
        var sqlString = await CreateSqlTextAsync(view.DataSource);

        return await _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms
             .AddDateFromQuery("@From",  prms, "From")
             .AddDateFromQuery("@To", prms, "To")
             .AddBitFromQuery("@Run", prms, "Run")
             .AddStringFromQuery("@Tab", prms, "Tab");
            foreach (var r in _grouping.Filters)
                dbprms.AddBigIntFromQuery($"@{r.Column}", prms, r.Column);
        });
    }

    private async Task<String> CreateSqlTextAsync(String? dataSource)
    {
        var appMeta = await _metadataProvider.GetAppMetadataAsync(dataSource);

        var filterFields = _grouping.Filters.Select(f =>
            $"[{f.Column}!T{f.Column}!RefId] = @{f.Column}"
        );

        var filterMaps = new StringBuilder();
        foreach (var f in _grouping.Filters)
        {
            var refMeta = await _metadataProvider.GetSchemaAsync(dataSource, f.RefSchema, f.RefTable);
            filterMaps.AppendLine($"""
                select [!T{f.Column}!Map] = null, [Id!!Id] = [{refMeta.PrimaryKeyField}], [Name!!Name] = [{refMeta.NameField}]
                from {f.RefSchema}.[{f.RefTable}]
                where [{refMeta.PrimaryKeyField}] = @{f.Column}
            """);
        }

        IEnumerable<String> createTempTableFeilds()
        {
            foreach (var f in _grouping.Filters.Union(_grouping.Grouping).Distinct(Comparers.ReportItemMetadata))
                yield return f.CreateField(appMeta.IdDataType);
            foreach (var f in _grouping.Data)
            {
                yield return f.CreateField(appMeta.IdDataType, "Start");
                yield return f.CreateField(appMeta.IdDataType, "In");
                yield return f.CreateField(appMeta.IdDataType, "Out");
            }
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        drop table if exists #tmpturn;
        create table #tmpturn({String.Join(',', createTempTableFeilds())});
                
        declare @now date = getdate();
        set @From = isnull(@From, datefromparts(year(@now), month(@now), 1));
        set @To = isnull(@To, eomonth(@From));
        declare @end date = dateadd(day, 1, @To);    

        insert into #tmpturn({_grouping.SimpleFields()}, {_grouping.SimpleDataFields()})
        select {_grouping.SelectFields("j")},
            {_grouping.InsertIntoDataFields("j")}
        from {_source.SqlTableName} j
        where @Run = 1 and [Date] < @end
            {_grouping.SqlWhereClause("j")} 
        group by {_grouping.SelectFields("j")};

        -- Main Select
        with T as (
            select 
                {_grouping.FieldsWithGrouping()},
                {_grouping.AggregateDataFields()}
            from #tmpturn
            where @Run = 1
            group by rollup({_grouping.SimpleFields()})
        )
        select [RepData!TRepData!Group] = null, 
            {_grouping.ReferenceWithGrouping()},
            {_grouping.FullDataFields()}, 
            [Items!TRepData!Items] = null
        from T
            {_grouping.ReferenceJoins()}
        where @Run = 1
        order by {_grouping.ReferenceOrderByGrp()};

        select [Filter!TFilter!Object] = null, {String.Join(',', filterFields)},
            [Group] = N'{_grouping.GroupParams}',
            [Period.From!TPeriod!] = @From, [Period.To!TPeriod!] = @To, Run = @Run, Tab = @Tab;

        {filterMaps}

        {_grouping.RepInfoSql()}
        """;

        return sqlString;
    }

    public override UIElement CreatePage()
    {
        var dataCount = _grouping.Data.Count();
        var headerRowCount = dataCount > 1 ? 2 : 1;

        IEnumerable<SheetCell> dataCells()
        {
            yield return new SheetGroupCell();
            yield return new SheetCell()
            {
                GroupIndent = true,
                ColSpan = 2,
                Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("$groupName"))
            };
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("Start");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("In");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("Out");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("End");
        }

        IEnumerable<SheetCell> totalCells()
        {
            yield return new SheetCell();
            yield return new SheetCell()
            {
                ColSpan = 2,
                Content = "@[Total]"
            };
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.Start");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.In");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.Out");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.End");
        }

        IEnumerable<SheetCell> headerCellsRow1()
        {
            yield return new SheetCell()
            {
                RowSpan = headerRowCount,
            };
            yield return new SheetCell()
            {
                ColSpan = 2,
                RowSpan = headerRowCount,
                Content = String.Join(" / ", _grouping.Grouping.Select(g => g.LocalizeLabel()))
            };
            foreach (var d in "@[TurnOver.Start]|@[TurnOver.In]|@[TurnOver.Out]|@[TurnOver.End]".Split('|'))
            {
                yield return new SheetCell()
                {
                    ColSpan = dataCount,
                    Content = d
                };
            }
        }

        IEnumerable<SheetCell> headerCellsRow2()
        {
            foreach (var r in Enumerable.Range(1, 4))
                foreach (var d in _grouping.Data)
                    yield return new SheetCell()
                    {
                        Content = d.LocalizeLabel(),
                        Wrap = WrapMode.NoWrap
                    };
        }

        IEnumerable<SheetRow> headerRows()
        {
            yield return new SheetRow()
            {
                Style = RowStyle.Header,
                Cells = [.. headerCellsRow1()]
            };
            if (dataCount > 1)
            {
                yield return new SheetRow()
                {
                    Style = RowStyle.Header,
                    Cells = [..headerCellsRow2()]
                };
            }
        }

        SheetSection TotalSection()
        {
            return new SheetSection()
            {
                Children = [
                    new SheetRow()
                    {
                        Style = RowStyle.Total,
                        Cells = [..totalCells()]
                    }
                ]
            };
        }

        var page = new Page()
        {
            Title = _report?.ItemLabel.Localize() ?? "@[Report]",
            CssClass = "report-page",
            UserSelect = true,
            Background = BackgroundStyle.White,
            Toolbar = CreateToolbar(),
            Taskpad = CreateTaskpad(),
            Children = [
                new Block() {
                    CssClass = "sheet-page sheet-report",
                    Bindings = b => b.SetBinding(nameof(Block.CssClass), new Bind("Root.$SheetPageClass")),
                    Children = [
                        new Sheet()
                        {
                            GridLines = GridLinesVisibility.Both,
                            Bindings = b => b.SetBinding(nameof(Sheet.If), new Bind("Filter.Run")),
                            Sections = [
                                new SheetSection() {
                                    Children = [..headerRows()]
                                },
                                TotalSection(),
                                new SheetTreeSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData.Items")),
                                    Children = [
                                        new SheetRow() {
                                            Cells = [..dataCells()]
                                        }
                                    ]
                                }
                            ],
                            Header = [..CreateSheetHeader()],
                            Columns = [
                                new SheetColumn() {
                                    Width = Length.FromString("16px"),
                                },
                                new SheetColumn() {
                                    Width = Length.FromString("4rem"),
                                },
                                new SheetColumn() {
                                    Width = Length.FromString("100%"),
                                    MinWidth = Length.FromString("Auto")
                                }
                            ]
                        }
                    ]
                },
                CreateNonRunPanel()
            ]
        };

        return page;
    }
}
