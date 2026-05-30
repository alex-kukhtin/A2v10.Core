// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Data;
using A2v10.Infrastructure;
using A2v10.Xaml.DynamicRendrer;
using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Platform.Web;

public class AuxMenuEndpointHandler(IServiceProvider _serviceProvider, ILocalizer _localizer) : IEndpointHandler
{
    private readonly IServiceProvider _xamlSericeProvider = new XamlServiceProvider();
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
    private readonly IAppCodeProvider _codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();

    public async Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
    {
        using var stream = _codeProvider.FileStreamRO("menu.json", true)
            ?? throw new InvalidOperationException("menu.json not found");
        using var sr = new StreamReader(stream);
        var json = await sr.ReadToEndAsync()
            ?? throw new InvalidOperationException("menu.json is empty");
        json = _localizer.Localize(null, json, false)
            ?? throw new InvalidOperationException("menu.json is empty");

        var menu = JsonConvert.DeserializeObject<JsonMenuRoot>(json)
            ?? throw new InvalidOperationException("deserialize menu.json fail");

        var menuId = platformUrl.Query?.Get<String>("Mode")
            ?? throw new InvalidOperationException("Query parameter 'Mode' not found");
        var menuItem = JsonMenu.FindById(menu.Menu, menuId)
            ?? throw new InvalidOperationException($"Aux menu for {menuId} not found");
        String rootId = $"el{Guid.NewGuid()}";

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = CreatePage(menuItem),
            ModelView = modelView,
            PlatformUrl = platformUrl,
            Model = CreateDataModel(menuItem, platformUrl.Id ?? String.Empty),
            Template = CreateTemplate()
        };
        return await _dynamicRenderer.RenderPage(rri);
    }


    IDataModel CreateDataModel(JsonMenu menu, String id)
    {
        var db = new DataModelBuilder();
        db.AddSystem("Id", id == "any" ? String.Empty : id);
        var tRoot = db.AddMetadata("TRoot");
        tRoot.AddField("Menu", "TMenuArray");

        var tRow = db.AddMetadata("TMenu");
        tRow.IsArrayType = true;
        tRow.AddField("Id", SqlDataType.String, 255)
            .AddField("Url", SqlDataType.String, 255)
            .AddField("Title", SqlDataType.String, 255)
            .AddField("Category", SqlDataType.String, 255)
            .SetId("Id")
            .SetName("Title");

        var root = new ExpandoObject()
        {
            { "Menu", menu.Items?.Select(mi => new ExpandoObject()
                {
                    { "Id", mi.IdFromUrl() },
                    { "Title", mi.Title },
                    { "Category", mi.Category },
                    { "Url", Path.Combine(mi.Url ?? "/", "indexpartial/0") }
                })
            }
        };
        return db.CreateDataModel(root);
    }
    private Page CreatePage(JsonMenu menu)
    {
        var page = new Page()
        {
            Children = [
                new Grid(_xamlSericeProvider)
                {
                    Height = Length.FromString("100%"),
                    Columns = ColumnDefinitions.FromString("MinMax(20rem;25%),1*"),
                    AlignItems = AlignItem.Stretch,
                    Gap = GapSize.FromString("1rem"),
                    Children = [
                        new List() {
                            GroupBy = "Category",
                            Border = true,
                            Background = BackgroundStyle.White,
                            AutoSelect = AutoSelectMode.ItemId,
                            Bindings = b => b.SetBinding(nameof(List.ItemsSource), new Bind("Menu")),
                            Attach = att => {
                                att.Add("Grid.Col", "1");
                            },
                            Content = [
                                new ListItemSimple() {
                                    Icon=Icon.List,
                                    Bindings = b => b.SetBinding(nameof(ListItemSimple.Content), new Bind("Title"))
                               }
                            ]
                        },
                        new Include() {
                            FullHeight = true,
                            Attach = att => {
                                att.Add("Grid.Col", "2");
                            },
                            Bindings = b => {
                                b.SetBinding(nameof(Include.If), new Bind("Menu.$hasSelected"));
                                b.SetBinding(nameof(Include.Source), new Bind("Menu.$selected.Url"));
                            }
                        }
                    ]
                }
            ]
        };

        return page;
    }

    private String CreateTemplate()
    {
        return String.Empty;
    }

    public Task<IDataModel> ReloadAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
    {
        throw new NotSupportedException();
    }

    public Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject data, ExpandoObject prms)
    {
        throw new NotSupportedException();
    }
}
