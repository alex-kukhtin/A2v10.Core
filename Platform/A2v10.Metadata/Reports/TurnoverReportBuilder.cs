// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class TurnoverReportBuilder(IServiceProvider serviceProvider, TableMetadata report, TableMetadata source) 
    : BaseReportBuilder(serviceProvider, report, source)
{
    public override Task<IDataModel> LoadReportModelAsync(IModelView view, ExpandoObject prms)
    {
        var sqlString = CreateSqlText();

        return _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
            dbprms
             .AddDateFromQuery("@From",  prms, "From")
             .AddDateFromQuery("@To", prms, "To")
             .AddBitFromQuery("@Run", prms, "Run")
             .AddStringFromQuery("@Tab", prms, "Tab");
            // dbPrms.AddRefereces
        });
    }

    private String CreateSqlText()
    {
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        drop table if exists #tmpturn;
        
        declare @now date = getdate();
        set @From = isnull(@From, datefromparts(year(@now), month(@now), 1));
        set @To = isnull(@To, eomonth(@From));
        declare @end date = dateadd(day, 1, @To);    

        select [RepData!TRepData!Array] = null, [Id!!Id] = [Id], [Date], InOut, [Sum], Qty
        from {_source.SqlTableName}
        where [Date] > @From and [Date] < @end;

        select [Filter!TFilter!Object] = null,
            [Period.From!TPeriod!] = @From, [Period.To!TPeriod!] = @To, Run = @Run, Tab = @Tab;
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
                                new SheetSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData")),
                                    Children = [
                                        new SheetRow() {
                                            Cells = [
                                                new SheetCell() {Content = "1"},
                                                new SheetCell() {Content = "2"},
                                                new SheetCell() {
                                                    Align  = TextAlign.Center,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Date") {DataType = DataType.Date})
                                                },
                                                new SheetCell() {
                                                    Align  = TextAlign.Right,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new BindNumber("Qty"))
                                                },
                                                new SheetCell() {
                                                    Align  = TextAlign.Right,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new BindSum("Sum"))
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
