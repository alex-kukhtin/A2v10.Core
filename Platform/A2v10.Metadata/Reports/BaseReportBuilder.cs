// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;

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
    protected readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();
    public abstract Task<IDataModel> LoadReportModelAsync(IModelView view, ExpandoObject prms);

    // TODO: move to A2v10.Data.Core - QueryParameterDate
    protected static DateTime? DateParameter(ExpandoObject eo, String prop)
    {
        var val = eo.Get<String>(prop);
        if (val == null)
            return null;
        return DateTime.ParseExact(val, "yyyyMMdd", CultureInfo.InvariantCulture);
    }

    // TODO: move to A2v10.Data.Core - QueryParameterBit
    protected static Boolean? BoolParameter(ExpandoObject eo, String prop)
    {
        var val = eo.Get<Object?>(prop);
        if (val == null)
            return null;
        if (val is Boolean boolVal)
            return boolVal;
        else if (val is String strVal)
            return Convert.ToBoolean(strVal, CultureInfo.InvariantCulture);
        throw new InvalidOperationException($"Invalid type for bit {val.GetType()}");
    }

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
                        Content = "@[Run]",
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
                                new Span() { Space = SpaceMode.After, Content = "@[Report.Param.Changed" },
                                new Hyperlink() {
                                    Content = "@[RunNow]",
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
        }
        return new Taskpad()
        {
            CssClass = "report-taskpad bg-primary",
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
                                    }
                                ]
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
                    new Span() { Content = "@[ReportNotYetBuild]", Block = true },
                    new Hyperlink() {
                        Content = "@[RunNow]",
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
        
            let filter = {
                Run : true,
                Tab : repFilter.Tab,
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
