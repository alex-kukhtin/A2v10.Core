// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;
using A2v10.System.Xaml;

namespace A2v10.Metadata;

internal abstract class BaseReportBuilder(IServiceProvider serviceProvider, TableMetadata report, TableMetadata source)
{
    protected IServiceProvider _serviceProvider = serviceProvider;
    protected TableMetadata _report => report;
    protected TableMetadata _source => source;
    protected IDbContext _dbContext => _serviceProvider.GetRequiredService<IDbContext>();
    protected ICurrentUser _currentUser => _serviceProvider.GetRequiredService<ICurrentUser>();
    protected DatabaseMetadataProvider _metadataProvider => _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();

    protected readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    protected ReportGrouping _grouping = default!;

    public abstract Task<IDataModel> LoadReportModelAsync(IModelView view, ExpandoObject prms);
    public abstract UIElement CreatePage();
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

            foreach (var r in _report.TypedReportItems(ReportItemKind.Filter))
            {
                yield return new SelectorSimple()
                {
                    Label = r.LocalizeLabel(),
                    Url = r.Endpoint(),
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
    protected EmptyPanel CreateNonRunPanel()
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
    public virtual String CreateTemplate()
    {
        return """
        const utils = require("std:utils");
        const template = {
            options: {
                noDirty: true
            },
            properties: {
                'TRoot.$$Dirty': Boolean,
                'TRoot.$$Loading': Boolean,
                'TRoot.$AlertVisible'() { return this.$$Dirty && !this.$$Loading && this.Filter.Run; },
                'TRoot.$SheetPageClass'() { return this.$AlertVisible ? 'sheet-dirty' : undefined }
            },
            commands: {
                generate
            },
            events: {
                'Model.dirty.change': modelChanged
            }
        };

        module.exports = template;

        function modelChanged(dirty, prop) {
        	if (!dirty) return;
        	if (prop === 'Filter.Tab')
        		return;
        	this.$$Dirty = true;
        }
        
        function generate() {
            const ctrl = this.$ctrl;
            
            let repFilter = this.Filter;

            let group = this.GroupingInfo.filter(f => f.Checked).map(f => f.Id).join('!');
            let datinfo = this.DataInfo.filter(f => f.Checked).map(f => f.Id).join('!');

            if (!group) {
                ctrl.$alert('@[Report.Error.AtLeastGroup]');
                return;
            }
        
            if (!datinfo) {
                ctrl.$alert('@[Report.Error.AtLeastData]');
                return;
            }
                
            let filter = {
                Run : true,
                Group: group,
                Data: datinfo,
                Tab : repFilter.Tab
            };

            this.$$Loading = true;
            let defNames = ['Run', 'Tab'];

            for (let fld of Object.getOwnPropertyNames(repFilter)
                .filter(f => !f.startsWith('_') && !f.startsWith('$') && !defNames.includes(f))) {
                if (fld === 'Period')
                    filter[fld] = repFilter.Period.format('DateUrl');
                else if (utils.isObjectExact(repFilter[fld]))   
                    filter[fld] = repFilter[fld].Id;
            }
            ctrl.$requery(filter);
        }
        
        """;
    }

}
