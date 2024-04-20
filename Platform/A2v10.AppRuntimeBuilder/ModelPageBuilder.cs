// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using A2v10.System.Xaml;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;

namespace A2v10.AppRuntimeBuilder;

internal class ModelPageBuilder(IServiceProvider _serviceProvider)
{
	private readonly IServiceProvider _xamlSericeProvider = new XamlServiceProvider();
	private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
	private readonly IAppCodeProvider _codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
	private readonly IXamlPartProvider _xamlPartProvider = _serviceProvider.GetRequiredService<IXamlPartProvider>();
	public async Task<String> RenderPageAsync(IPlatformUrl platformUrl, IModelView modelView, EndpointDescriptor endpoint, IDataModel dataModel)
	{
		String rootId = $"el{Guid.NewGuid()}";

		String templateText = String.Empty;
		if (!String.IsNullOrEmpty(modelView.Template))
			templateText = await GetTemplateScriptAsync(modelView);
		else if (modelView.IsIndex)
			templateText = CreateIndexTemplate(endpoint);
		else if (platformUrl.Action == "edit")
			templateText = CreateEditTemplate(endpoint);

		UIElement? page = null;

		var rawView = modelView.GetRawView(false);
		if (!String.IsNullOrEmpty(rawView))
			page = LoadPage(modelView, rawView);
		else if (modelView.IsIndex && !modelView.IsDialog)
			page = CreateIndexPage(platformUrl, endpoint);
		else if (!modelView.IsIndex && modelView.IsDialog && platformUrl.Action == "edit")
			page = CreateEditDialog(endpoint);
		else if (!modelView.IsIndex && !modelView.IsDialog && platformUrl.Action == "edit")
			page = CreateEditPage(endpoint);
		else if (modelView.IsIndex && modelView.IsDialog && platformUrl.Action == "browse")
			page = CreateBrowseDialog(endpoint);

		if (page == null)
			throw new InvalidOperationException("Page is null");

		if (page is ISupportPlatformUrl supportPlatformUrl)
			supportPlatformUrl.SetPlatformUrl(platformUrl);

		var rri = new DynamicRenderPageInfo()
		{
			RootId = rootId,
			Page = page,
			ModelView = modelView,
			PlatformUrl = platformUrl,
			Model = dataModel,
			Template = templateText
		};
		return await _dynamicRenderer.RenderPage(rri);
	}

	UIElement CreateIndexPage(IPlatformUrl platformUrl, EndpointDescriptor endpoint)
	{
		var arrayName = endpoint.BaseTable.Name;
		var indexUi = endpoint.GetIndexUI();
		var editMode = endpoint.EditMode();

		BindCmd EditCommand()
		{
			var cmd = new BindCmd();
			if (editMode == EndpointEdit.Dialog)
			{
				cmd.Command = CommandType.Dialog;
				cmd.Action = DialogAction.EditSelected;
				cmd.Url = $"/{platformUrl.LocalPath}/edit";
				cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(arrayName));
			}
			else if (editMode == EndpointEdit.Page) {
                cmd.Command = CommandType.OpenSelected;
                cmd.Url = $"/{platformUrl.LocalPath}/edit";
                cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(arrayName));
            }
            return cmd;
		}

		BindCmd CreateCommand()
		{
            var cmd = new BindCmd();
            if (editMode == EndpointEdit.Dialog)
            {
                cmd.Command = CommandType.Dialog;
                cmd.Action = DialogAction.Append;
                cmd.Url = $"/{platformUrl.LocalPath}/edit";
                cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(arrayName));
            }
            else if (editMode == EndpointEdit.Page)
            {
                cmd.Command = CommandType.Append;
                cmd.Url = $"/{platformUrl.LocalPath}/edit";
                cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(arrayName));
            }
            return cmd;
        }

		var page = new Page()
		{
			CollectionView = new CollectionView()
			{
				RunAt = RunMode.ServerUrl,
				Filter = new FilterDescription()
				{
					Items = [
						new FilterItem() {
							DataType = DataType.String,
							Property = "Fragment"
						}
					]
				},
				Bindings = cw =>
				{
					cw.SetBinding(nameof(CollectionView.ItemsSource), new Bind(arrayName));
				}
			},
			Children = [
				new Grid(_xamlSericeProvider) {
					Rows = [
						new RowDefinition() {Height = GridLength.FromString("Auto")},
						new RowDefinition() {Height = GridLength.FromString("1*")},
						new RowDefinition() {Height = GridLength.FromString("Auto")},
					],
					Height = Length.FromString("100%"),
					Children = [
						new Toolbar(_xamlSericeProvider)
						{
							Children = [
								new Button() {
									Content = "@[Create]",
									Icon=Icon.Plus,
									Bindings = (btn) => {
										btn.SetBinding(nameof(Button.Command), CreateCommand());
									}
								},
								new Button() {
									Icon=Icon.Edit,
									Bindings = (btn) => {
										btn.SetBinding(nameof(Button.Command), EditCommand());
									}
								},
								new Separator(),
								XamlHelper.CreateButton(CommandType.Reload, Icon.Reload),
								new ToolbarAligner(),
								XamlHelper.CreateSearchBox()
							]
						},
						new DataGrid() {
							FixedHeader = true,
							Sort = true,
							Bindings = (dg) => {
								dg.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
							},
							Columns = indexUi.IndexColumns()
						},
						new Pager() {
							Bindings = p => {
								p.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"));
							}
						}
					]
				}
			]
		};
		return page;
	}

	UIElement CreateBrowseDialog(EndpointDescriptor endpoint)
	{
        var table = endpoint.BaseTable;
        var indexUi = endpoint.GetBrowseUI();
        var arrayName = endpoint.BaseTable.Name;

		var dlg = new Dialog()
		{
			Title = "Browse",
			Width = Length.FromString("60rem"),
			CollectionView = new CollectionView()
			{
				RunAt = RunMode.Server,
				Filter = new FilterDescription()
				{
					Items = [
						new FilterItem() {Property = "Fragment"}
					]
				},
				Bindings = cw => cw.SetBinding(nameof(CollectionView.ItemsSource), new Bind(table.Name))
			},
			Buttons = [
				new Button() {
					Style = ButtonStyle.Primary,
					Content = "@[Select]",
					Bindings = btn => {
						var bindCmd = new BindCmd("Select");
						bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(arrayName));
						btn.SetBinding(nameof(Button.Command), bindCmd);
					}
				},
				XamlHelper.CreateButton(CommandType.Close, "@[Cancel]"),
			],
			Children = [
				new Grid(_xamlSericeProvider) {
					Children = [
						new Toolbar(_xamlSericeProvider) {
							Children = [
								XamlHelper.CreateButton(CommandType.Reload, Icon.Reload),
								new ToolbarAligner(),
								XamlHelper.CreateSearchBox()
                            ]
                        },
						new DataGrid() {
							FixedHeader = true,
							Height = Length.FromString("35rem"),
							Columns = indexUi.IndexColumns(),
							Bindings = dg => dg.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"))
                        },
						new Pager() { Bindings = pgr => pgr.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))},
					]
				}
			]
		};
		return dlg;
	}

	UIElement CreateEditPage(EndpointDescriptor endpoint)
	{
        var uiElement = endpoint.GetEditUI();
        var table = endpoint.BaseTable;

        UIElementCollection PlainFields()
        {
            UIElementCollection coll = [];
            foreach (var f in uiElement.Fields)
                coll.Add(f.EditField(table));
            return coll;
        }

        var page = new Page()
		{
			Title = "Edit Page",
			Toolbar = new Toolbar(_xamlSericeProvider)
			{
				Children = [
					XamlHelper.CreateButton(CommandType.SaveAndClose, "@[SaveAndClose]", Icon.SaveCloseOutline),
                    XamlHelper.CreateButton(CommandType.Save, "@[Save]", Icon.SaveOutline),
					new Separator(),
					XamlHelper.CreateButton(CommandType.Reload, Icon.Reload),
					new ToolbarAligner(),
					XamlHelper.CreateButton(CommandType.Close, Icon.Close)
				]
			},
			Children = [
				new Grid(_xamlSericeProvider) {
					Width = Length.FromString("30rem"),
					Children = PlainFields()
				}
			]
		};
		return page;
	}

	UIElement CreateEditDialog(EndpointDescriptor endpoint)
	{
		var uiElement = endpoint.GetEditUI();
		var table = endpoint.BaseTable;
		UIElementCollection CreateDialogChildren()
		{
			UIElementCollection coll = [];
			foreach (var f in uiElement.Fields)
				coll.Add(f.EditField(table));
			return coll;
		}

		var dlg = new Dialog()
		{
			Title = "From Page",
			Overflow = true,
			Buttons = [
				new Button() {
					Content = "@[SaveAndClose]",
					Style = ButtonStyle.Primary,
					Bindings = (btn) => {
						btn.SetBinding(nameof(Button.Command),
							new BindCmd("SaveAndClose") {ValidRequired = true} );
					}
				},
				XamlHelper.CreateButton(CommandType.Close, "@[Cancel]")
			],
			Children = [
				new Grid(_xamlSericeProvider) {
					Children = CreateDialogChildren()
				}
			]
		};
		return dlg;
	}

	String CreateIndexTemplate(EndpointDescriptor endpoint)
	{
		var template = $$"""

			const template = {
				options:{
					noDirty: true,
					persistSelect: ['{{endpoint.BaseTable.Name}}']
				}
			};

			module.exports = template;            
			""";
		return template;
	}

	String CreateEditTemplate(EndpointDescriptor endpoint)
	{
		var ui = endpoint.GetEditUI();
		var table = endpoint.BaseTable;

		var rq = ui.Fields.Where(f => f.Required);

		var template = $$"""
			const template = {
				validators: {
					{{String.Join(",/n", rq.Select(f => $"'{table.ItemName()}.{f.Name}': '@[Error.Required]'"))}}
				}
			};

			module.exports = template;            
			""";
		return template;
	}

	private async Task<String> GetTemplateScriptAsync(IModelView view)
	{
		if (view.Path == null)
			throw new InvalidOperationException("Model.Path is null");
		var pathToRead = _codeProvider.MakePath(view.Path, $"{view.Template}.js");
		using var stream = _codeProvider.FileStreamRO(pathToRead)
			?? throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
		using var sr = new StreamReader(stream);
		var fileTemplateText = await sr.ReadToEndAsync() ??
			throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
		return fileTemplateText;
	}

	private UIElement LoadPage(IModelView modelView, String viewName)
	{
		var path = _codeProvider.MakePath(modelView.Path, viewName + ".xaml");
		var obj = _xamlPartProvider.GetXamlPart(path);
		if (obj is UIElement uIElement)
			return uIElement;
		throw new InvalidOperationException("Xaml. Root is not 'IXamlElement'");
	}
}
