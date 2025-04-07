// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;
using A2v10.Xaml.DynamicRendrer;
using A2v10.Xaml;
using A2v10.System.Xaml;


namespace A2v10.Metadata;

internal class ReportEndpointBuilder(IServiceProvider _serviceProvider, IModelBuilder _baseBuilder) : IMetaEndpointBuilder
{
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
    private readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    private readonly TableMetadata _source = _baseBuilder.Table;
    private readonly TableMetadata? _report = _baseBuilder.BaseTable
            ?? throw new InvalidOperationException("Report is null");
    private readonly AppMetadata _appMeta = _baseBuilder.AppMeta;
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
    {
        var dm = await LoadReportModelAsync(view);
        if (isReload)
            return new AppRuntimeResult(dm, null);
        String rootId = $"el{Guid.NewGuid()}";
        String templateText = CreateTemplate();

        UIElement page = CreatePage();

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(platformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = page,
            ModelView = view,
            PlatformUrl = platformUrl,
            Model = dm,
            Template = templateText
        };
        return new AppRuntimeResult(dm, await _dynamicRenderer.RenderPage(rri));
    }

    UIElement CreatePage() 
    {
        Toolbar createToolbar()
        {
            return new Toolbar(_xamlServiceProvider)
            {
                CssClass = "report-toolbar",
                Children = [
                    new Button() 
                    {
                        Icon = Icon.PlayOutline
                    },
                    new Separator(),
                    new Button()
                    {
                        Icon = Icon.Print
                    },
                    new Button()
                    {
                        Icon = Icon.ExportExcel
                    }
                ]
            };
        }

        Taskpad createTaskpad()
        {
            return new Taskpad()
            {
                CssClass = "report-taskpad",
                Children = [
                    new Grid(_xamlServiceProvider) 
                    {
                        Children = [
                            new TabBar() {
                                Buttons = [
                                    new TabButton() 
                                    {
                                        ActiveValue = "",
                                        Content = "@[Filters]"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };
        }

        return new Page()
        {
            Title = _report?.ItemName ?? "@[Report]",
            CssClass = "report-page",
            Toolbar = createToolbar(),
            Taskpad = createTaskpad(),
            Children = [
                new Sheet()
                {
                    GridLines = GridLinesVisibility.Both,
                    Sections = [
                        new SheetSection() {
                            Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData")),
                            Children = [
                                new SheetRow() {
                                    Cells = [
                                        new SheetCell() {Content = "1"},
                                        new SheetCell() {Content = "2"},
                                        new SheetCell() {
                                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Date") {DataType = DataType.Date})
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

    private Task<IDataModel> LoadReportModelAsync(IModelView view)
    {
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        select [RepData!TRepData!Array] = null, [Id!!Id] = [Id], [Date], InOut, [Sum], Qty
        from {_source.SqlTableName};
        
        """;
        return _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id);
        });
    }

    private String CreateTemplate()
    {
        return """
        const template = {
            properties: {
                'TRoot.$$Tab': String
            }
        };

        module.export = template;
        """;
    }
}
