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
using System.Collections;
using System.Collections.Generic;

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
                cmd.Command = CommandType.Create;
                cmd.Url = $"/{platformUrl.LocalPath}/edit";
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
								dg.SetBinding(nameof(DataGrid.DoubleClick), EditCommand());
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
							Bindings = dg => {
								var dblClick = new BindCmd() 
								{
									Command = CommandType.Select,
								};
								dblClick.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(table.Name));
								dg.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
								dg.SetBinding(nameof(DataGrid.DoubleClick), dblClick);
							}
                        },
						new Pager() { Bindings = pgr => pgr.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"))},
					]
				}
			]
		};
		return dlg;
	}

	UIElement CreateEditDetails(EditUiElement uiElement, String tableName)
	{
		TableCellCollection DetailsCells() 
		{
			var coll = new TableCellCollection();
			coll.Add(new TableCell()
			{
					Wrap = WrapMode.NoWrap,
					Bindings = tc => tc.SetBinding(nameof(TableCell.Content), new Bind("RowNo"))
			}
			); ;
			foreach (var elem in uiElement.Fields.Where(f => f.Name != "RowNo"))
			{
				coll.Add(new TableCell()
				{
					Content = elem.EditCellField(),
				});
			}
			coll.Add(new Hyperlink()
			{
				Content = "✕",
				Wrap = WrapMode.NoWrap,
				Bindings = (h) =>
				{
					var bindCmd = new BindCmd("Remove");
					bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind());
					h.SetBinding(nameof(Hyperlink.Command), bindCmd);
				}
			});
			return coll;		
		}

		TableCellCollection HeaderCells()
		{
            var coll = new TableCellCollection();
            coll.Add(new TableCell()
            {
                Wrap = WrapMode.NoWrap,
				Content = "#"
            }
            ); ;
            foreach (var elem in uiElement.Fields.Where(f => f.Name != "RowNo"))
            {
                coll.Add(new TableCell()
                {
                    Content = elem.RealTitle(),
                });
            }
            coll.Add(new TableCell());
			return coll;
        }

		TableColumnCollection TableColumns()
		{
			TableColumnCollection tc = [];
			foreach (var elem in uiElement.Fields)
				tc.Add(new TableColumn() { Width = elem.XamlColumnWidth()});
			tc.Add(new TableColumn() { Fit = true });
            return tc;
		}

		return new Block()
		{
			Children = [
				new Toolbar(_xamlSericeProvider) {
					Children = [
						new Button() {
							Icon= Icon.Plus,
							Bindings = btn => {
								var bindCmd = new BindCmd() {
									Command = CommandType.Append,
								};
								bindCmd.BindImpl.SetBinding(nameof(bindCmd.Argument), new Bind($"{tableName}.{uiElement.Name}"));
								btn.SetBinding(nameof(Button.Command), bindCmd);
							}
						}
					]
				},
				new Table() {
					GridLines = GridLinesVisibility.Both,
					StickyHeaders = true,
					Background = TableBackgroundStyle.Paper,
					Width = Length.FromString("100%"),
					Columns = TableColumns(),
					Header = [
						new TableRow() {
							Cells = HeaderCells()
						}
					],
					Rows = [
						new TableRow() {
							Cells = DetailsCells()
						}
					],
					Footer = [
						new TableRow() {
							Cells = [
								new TableCell() { 
								}
							]
						}
					],
					Bindings = tbl => tbl.SetBinding(nameof(Table.ItemsSource), new Bind($"{tableName}.{uiElement.Name}"))
				}
			]
		};
	}

	UIElement CreateEditPage(EndpointDescriptor endpoint)
	{
        var uiElement = endpoint.GetEditUI();
        var table = endpoint.BaseTable;

        UIElementCollection PlainFields()
        {
            UIElementCollection coll = [];
            foreach (var f in uiElement.Fields)
                coll.Add(f.EditField(table.ItemName()));
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
		if (uiElement.Details != null)
		{
			foreach (var uiDetails in uiElement.Details)
			{
				page.Children.Add(CreateEditDetails(uiDetails, table.ItemName()));
			}
		}
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
				coll.Add(f.EditField(table.ItemName()));
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
		var validators = String.Join(",\n", rq.Select(f => $"'{table.ItemName()}.{f.Name}': '@[Error.Required]'"));

		IEnumerable<(RuntimeTable Table, UiField Field)> GetComputedFields()
		{
			foreach (var f in ui.Fields.Where(f => !String.IsNullOrEmpty(f.Computed)))
				yield return (table, f);
			if (ui.Details != null)
				foreach (var detailsTable in ui.Details)
					foreach (var f in detailsTable.Fields.Where(f => !String.IsNullOrEmpty(f.Computed)))
						yield return (detailsTable.BaseTable 
							?? throw new InvalidOperationException("BaseTable for Details is null"), 
							f);
		}

		var templateProps = String.Join(",\n", GetComputedFields().Select(f => $$"""
			'{{f.Table.TypeName()}}.{{f.Field.Name}}'() { return {{f.Field.Computed}}; }
		"""));

		var template = $$"""
			const template = {
				properties:{
					{{templateProps}}
				},
				validators: {
					{{validators}}
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
