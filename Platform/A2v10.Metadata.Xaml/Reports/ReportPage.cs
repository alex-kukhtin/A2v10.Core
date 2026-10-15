// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

using A2v10.Xaml;
using A2v10.System.Xaml;

namespace A2v10.Metadata;

/* The page of a report, the XAML half of a report type; the query and the template are the builder's
 * (BaseReportBuilder). Drawn from the declaration and the picks of the url - a ReportGrouping of its
 * own, built from the same query the builder's is, so the two cannot see different picks.
 */
internal abstract class ReportPage(ReportMetadata report, ReportGrouping grouping)
{
    protected readonly ReportMetadata _report = report;
    protected readonly ReportGrouping _grouping = grouping;
    protected readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    /* The page half of what a report TYPE means; the query half is BaseReportBuilder.Create. The
     * validator builds both for every report, so a type taught to one of the two fails there.
     */
    internal static ReportPage Create(ReportEndpointMetadata endpoint, ExpandoObject prms)
    {
        var grouping = new ReportGrouping(endpoint.Report, endpoint.Surface, prms);
        return endpoint.Report.Type switch
        {
            "turnover" => new TurnoverReportPage(endpoint.Report, grouping),
            "trialBalance" => new TrialBalanceReportPage(endpoint.Report, grouping),
            "chessboard" => new ChessboardReportPage(endpoint.Report, grouping),
            _ => throw new NotImplementedException($"Invalid Report type '{endpoint.Report.Type}' for {endpoint.Path}")
        };
    }

    public abstract UIElement CreatePage();

    // what the user picks besides the period and the filters: groups and data; a fixed report has none
    protected virtual Boolean HasChoices => true;

    protected Toolbar CreateToolbar()
    {
        return new Toolbar(_xamlServiceProvider)
        {
            CssClass = "report-toolbar bg-primary",
            Children = [
                new Button()
                    {
                        Icon = Icon.PlayOutline,
                        Content = "@[Report.Run]",
                        Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmdExec("generate"))
                    },
                    new Separator(),
                    new Button()
                    {
                        Icon = Icon.Print,
                        Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd() { Command = CommandType.Print })
                    },
                    new Button()
                    {
                        Icon = Icon.ExportExcel,
                        Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd() {
                            Command = CommandType.ExportTo,
                            Format = ExportToFormat.Excel,
                            FileName = _report?.ItemLabel.Localize() ?? "@Report"
                        })
                    },
                    new ToolbarAligner(),
                    new Alert() {
                        Style = AlertStyle.Warning,
                        DropShadow = ShadowStyle.Shadow1,
                        Margin = Thickness.FromString("0,.5rem,0,0"),
                        Bindings = b => b.SetBinding(nameof(Alert.If), new Bind("Root.$AlertVisible")),
                        Content = new Text() 
                        {
                            Inlines = [
                                new Span() { Space = SpaceMode.After, Content = "@[Report.Param.Changed]" },
                                new Hyperlink() {
                                    Content = "@[Report.RunNow]",
                                    Bindings = b => b.SetBinding(nameof(Hyperlink.Command), new BindCmdExec("generate"))
                                },
                            ]
                        }
                    }
            ]
        };
    }
    protected Taskpad CreateTaskpad()
    {
        IEnumerable<UIElement> filters()
        {
            yield return new PeriodPicker()
            {
                Label = "@[Period]",
                Placement = DropDownPlacement.BottomRight,
                Display = DisplayMode.Name,
                Bindings = b => {
                    b.SetBinding(nameof(PeriodPicker.Value), new Bind("Filter.Period"));
                    b.SetBinding(nameof(PeriodPicker.Description), new Bind("Filter.Period.Name"));
                }
            };

            foreach (var r in _grouping.TypedReportItems(ReportItemKind.Filter))
            {
                yield return new SelectorSimple()
                {
                    Label = r.LocalizeLabel(),
                    Url = r.Endpoint,
                    ShowClear = true,
                    Highlight = true,
                    Placeholder = $"@[{r.Column}.AllData]",
                    Bindings = b =>
                    {
                        b.SetBinding(nameof(SelectorSimple.Value), new Bind($"Filter.{r.Column}"));
                    }
                };
            }
        }
        if (!HasChoices)
            return new Taskpad()
            {
                CssClass = "report-taskpad bg-primary",
                Width = Length.FromString("24rem"),
                Children = [
                    new Grid(_xamlServiceProvider) {
                        Children = [..filters()],
                    }
                ]
            };

        return new Taskpad()
        {
            CssClass = "report-taskpad bg-primary",
            Width = Length.FromString("24rem"),
            Children = [
                new Grid(_xamlServiceProvider)
                    {
                        Padding = Thickness.FromString("0"),
                        Children = [
                            new TabBar() {
                                Margin = Thickness.FromString("-9px,0,0,11px"),
                                Bindings = b => b.SetBinding(nameof(TabBar.Value), new Bind("Filter.Tab")),
                                Buttons = [
                                    new TabButton()
                                    {
                                        ActiveValue = "",
                                        Content = "@[Filters]",
                                    },
                                    new TabButton()
                                    {
                                        ActiveValue = "g",
                                        Content = "@[Grouping]"
                                    },
                                    new TabButton()
                                    {
                                        ActiveValue = "d",
                                        Content = "@[Data]"
                                    }
                                ]
                            },
                            new Switch() {
                                Bindings = b => b.SetBinding(nameof(Switch.Expression), new Bind("Filter.Tab")),
                                Cases = [
                                    new Case()
                                    {
                                        Value = "",
                                        Children = [
                                            new Grid(_xamlServiceProvider) {
                                                Children = [..filters()],
                                            }
                                        ]
                                    },
                                    new Case()
                                    {
                                        Value = "g",
                                        Children = [
                                            CreateGroupingPane()
                                        ]
                                    },
                                    new Case() {
                                        Value = "d",
                                        Children = [
                                            CreateDataPane()
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
            ]
        };
    }

    private Grid CreateGroupingPane()
    {
        var cmdUp = new BindCmd()
        {
            Command = CommandType.MoveSelected,
            CommandName = "up"
        };
        var cmdDown = new BindCmd()
        {
            Command = CommandType.MoveSelected,
            CommandName = "down"
        };
        cmdUp.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("GroupingInfo"));
        cmdDown.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("GroupingInfo"));

        return new Grid(_xamlServiceProvider)
        {
            Gap = GapSize.FromString("0"),
            AlignItems = AlignItem.Stretch,
            Columns = [
                new ColumnDefinition() { Width = GridLength.FromString("1*") },
                new ColumnDefinition() { Width = GridLength.FromString("Auto") }
            ],
            Children = [
                new List() {
                    Bindings = b => b.SetBinding(nameof(List.ItemsSource), new Bind("GroupingInfo")),
                    Width = Length.FromString("100%"),
                    Content = [
                        new StackPanel()
                        {
                            Gap = GapSize.FromString("6"),
                            Orientation = Orientation.Horizontal,
                            AlignItems = AlignItems.Center,
                            Children = [
                                new CheckBox() {
                                    Bindings = b => b.SetBinding(nameof(CheckBox.Value), new Bind("Checked"))
                                },
                                new Span() {
                                    Bindings = b => b.SetBinding(nameof(Span.Content), new Bind("Label"))
                                }
                            ]
                        }
                    ]
                },
                new Toolbar(_xamlServiceProvider) {
                    Orientation = ToolbarOrientation.Vertical,
                    Children = [
                        new Button() {
                            Icon = Icon.ArrowUp,
                            Bindings = b => b.SetBinding(nameof(Button.Command), cmdUp),
                        },
                        new Button() {
                            Icon = Icon.ArrowDown,
                            Bindings = b => b.SetBinding(nameof(Button.Command), cmdDown),
                        }
                    ]
                }
            ]
        };
    }

    private Grid CreateDataPane()
    {
        var cmdUp = new BindCmd()
        {
            Command = CommandType.MoveSelected,
            CommandName = "up"
        };
        var cmdDown = new BindCmd()
        {
            Command = CommandType.MoveSelected,
            CommandName = "down"
        };
        cmdUp.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("DataInfo"));
        cmdDown.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind("DataInfo"));

        return new Grid(_xamlServiceProvider)
        {
            Gap = GapSize.FromString("0"),
            AlignItems = AlignItem.Stretch,
            Columns = [
                new ColumnDefinition() { Width = GridLength.FromString("1*") },
                new ColumnDefinition() { Width = GridLength.FromString("Auto") }
            ],
            Children = [
                new List() {
                    Bindings = b => b.SetBinding(nameof(List.ItemsSource), new Bind("DataInfo")),
                    Width = Length.FromString("100%"),
                    Content = [
                        new StackPanel()
                        {
                            Gap = GapSize.FromString("6"),
                            Orientation = Orientation.Horizontal,
                            AlignItems = AlignItems.Center,
                            Children = [
                                new CheckBox() {
                                    Bindings = b => b.SetBinding(nameof(CheckBox.Value), new Bind("Checked"))
                                },
                                new Span() {
                                    Bindings = b => b.SetBinding(nameof(Span.Content), new Bind("Label"))
                                }
                            ]
                        }
                    ]
                },
                new Toolbar(_xamlServiceProvider) {
                    Orientation = ToolbarOrientation.Vertical,
                    Children = [
                        new Button() {
                            Icon = Icon.ArrowUp,
                            Bindings = b => b.SetBinding(nameof(Button.Command), cmdUp),
                        },
                        new Button() {
                            Icon = Icon.ArrowDown,
                            Bindings = b => b.SetBinding(nameof(Button.Command), cmdDown),
                        }
                    ]
                }
            ]
        };
    }
    protected static EmptyPanel CreateNonRunPanel()
    {
        return new EmptyPanel()
        {
            Icon = Icon.WarningOutline,
            Margin = Thickness.FromString("10rem,0"),
            Content = new Text()
            {
                Inlines = [
                    new Span() { Content = "@[Report.NotYetBuild]", Block = true },
                    new Hyperlink() {
                        Content = "@[Report.RunNow]",
                        Bindings = b => b.SetBinding(nameof(Hyperlink.Command), new BindCmdExec("generate"))
                    }
                ]
            },
            Bindings = b => b.SetBinding(nameof(EmptyPanel.If), new Bind("!Filter.Run")),
        };
    }

    protected IEnumerable<SheetRow> CreateSheetHeader()
    {
        IEnumerable<SheetRow> ParameterRows()
        {
            yield return new SheetRow()
            {
                Style = RowStyle.Parameter,
                Cells = [
                    new SheetCell() {
                        ColSpan = 2,
                        Content = "@[Period]"
                    },
                    new SheetCell() {
                        ColSpan = 4,
                        Bold = true,
                        Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Filter.Period.Name"))
                    }
                ]
            };
            foreach (var p in _grouping.Filters)
            {
                yield return new SheetRow()
                {
                    Style = RowStyle.Parameter,
                    Cells = [
                        new SheetCell() {
                            ColSpan = 2,
                            Wrap = WrapMode.NoWrap,
                            Content = p.LocalizeLabel()
                        },
                        new SheetCell() {
                            ColSpan = 4,
                            Bold = true,
                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind($"Filter.{p.Column}.Name"))
                        }
                    ],
                    Bindings = b => b.SetBinding(nameof(SheetRow.If), new Bind($"Filter.{p.Column}.Id"))
                };
            }
        }

        yield return new SheetRow()
        {
            Style = RowStyle.Title,
            Cells = [
                new SheetCell() { 
                    ColSpan = 6,
                    Content = _report.ItemLabel.Localize()
                }
            ]
        };
        foreach (var r in ParameterRows())
            yield return r;
        yield return new SheetRow()
        {
            Style = RowStyle.Divider
        };
    }
}
