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
        var filters = _report.TypedReportItems(ReportItemKind.Filter);

        var sqlString = await CreateSqlTextAsync(view.DataSource);

        return await _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms
             .AddDateFromQuery("@From",  prms, "From")
             .AddDateFromQuery("@To", prms, "To")
             .AddBitFromQuery("@Run", prms, "Run")
             .AddStringFromQuery("@Tab", prms, "Tab");
            foreach (var r in filters)
                dbprms.AddBigIntFromQuery($"@{r.Column}", prms, r.Column);
        });
    }

    private async Task<String> CreateSqlTextAsync(String? dataSource)
    {
        var appMeta = await _metadataProvider.GetAppMetadataAsync(dataSource);

        var filterElems = _report.TypedReportItems(ReportItemKind.Filter).ToList();
        var groupingElems = _report.TypedReportItems(ReportItemKind.Grouping).ToList();
        var dataElems = _report.TypedReportItems(ReportItemKind.Data).ToList();

        var filterFields = filterElems.Select(f =>
            $"[{f.Column}!T{f.Column}!RefId] = @{f.Column}"
        );

        var filterMaps = new StringBuilder();
        foreach (var f in filterElems)
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
            foreach (var f in filterElems.Union(groupingElems).Distinct(Comparers.ReportItemMetadata))
                yield return f.CreateField(appMeta.IdDataType);
            foreach (var f in dataElems)
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

        -- TODO: Insert rems, turns
        insert into #tmpturn([Store], [InSum], [OutSum])
        select [Store], 
            InSum = sum(case when InOut = 1 then [Sum] else 0 end),
            OutSum = sum(case when InOut = -1 then [Sum] else 0 end)
        from {_source.SqlTableName}
        where [Date] > @From and [Date] < @end
        group by [Store];

        with T as (
            -- Main Select
            select [Store], [InSum] = sum(InSum), [OutSum] = sum(OutSum),
            StoreGrp = grouping([Store])
            from #tmpturn
            group by rollup([Store])
        )
        select [RepData!TRepData!Group] = null, 
            [Store.Id!TStore!Id] = T.Store, [Store.Name!TStore!Name] = isnull(s.[Name], N'@[Store.NoData]'), [InSum], OutSum,
            [Store!!GroupMarker] = StoreGrp,
            [Items!TRepData!Items] = null
        from T
            left join cat.Stores s on T.Store = s.Id
        order by StoreGrp desc;

        -- TODO: remove
        select [Temp!TTemp!Array] = null, *
        from #tmpturn;

        select [Filter!TFilter!Object] = null, {String.Join(',', filterFields)},
            [Period.From!TPeriod!] = @From, [Period.To!TPeriod!] = @To, Run = @Run, Tab = @Tab;

        {filterMaps}
        """;

        return sqlString;
    }

    public override UIElement CreatePage()
    {
        SheetSection HeaderSection()
        {
            return new SheetSection()
            {
                Children = [
                    new SheetRow()
                    {
                        Style = RowStyle.Header,
                        Cells = [
                            new SheetCell() {
                                ColSpan = 2,
                                Content = "H1"
                            },
                            new SheetCell() {
                                Content = "H2"
                            },
                            new SheetCell() {
                                Content = "H3"
                            }
                        ]
                    }
                ]
            };
        }

        SheetSection TotalSection()
        {
            return new SheetSection()
            {
                Children = [
                    new SheetRow()
                    {
                        Style = RowStyle.Total,
                        Cells = [
                            new SheetCell() {
                                ColSpan = 2,
                                Content = "Total"
                            },
                            new SheetCell() {
                                Content = "T2"
                            },
                            new SheetCell() {
                                Content = "T3"
                            }
                        ]
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
                                HeaderSection(),
                                TotalSection(),
                                new SheetTreeSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData.Items")),
                                    Children = [
                                        new SheetRow() {
                                            Cells = [
                                                new SheetGroupCell(),
                                                new SheetCell() {Content = "1", GroupIndent = true },
                                                new SheetCell() {Content = "2" },
                                                new SheetCell() {
                                                    Align  = TextAlign.Center,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("$groupName"))
                                                },
                                                new SheetCell() {
                                                    Align  = TextAlign.Right,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new BindSum("InSum"))
                                                },
                                                new SheetCell() {
                                                    Align  = TextAlign.Right,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new BindSum("OutSum"))
                                                },
                                                new SheetCell() {
                                                    Align  = TextAlign.Right,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Id"))
                                                },
                                            ]
                                        }
                                    ]
                                }
                            ],
                            Header = [..CreateSheetHeader()],
                            Columns = [
                                new SheetColumn() {
                                    Width = Length.FromString("1rem"),
                                },
                                new SheetColumn() {
                                    Width = Length.FromString("1rem"),
                                },
                                new SheetColumn() {
                                    Width = Length.FromString("5rem"),
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
