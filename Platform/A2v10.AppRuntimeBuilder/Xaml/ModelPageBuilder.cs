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
			templateText = TemplateBuilder.CreateIndexTemplate(endpoint);
		else if (platformUrl.Action == "edit")
			templateText = TemplateBuilder.CreateEditTemplate(endpoint);

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
		var filters = indexUi.Fields.Where(f => f.Filter && !f.Name.Contains('.'));

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

		FilterItems CreateFilters()
		{
			FilterItems f = [
				new FilterItem()
				{
					DataType = DataType.String,
					Property = "Fragment"
				}
			];
			foreach (var field in filters)
				f.Add(new FilterItem()
				{
					Property = field.IsPeriod() ? "Period" : field.Name,
					DataType = field.IsPeriod() ? DataType.Period : DataType.Object,
				});
			return f;
		}
		var page = new Page()
		{
			CollectionView = new CollectionView()
			{
				RunAt = RunMode.ServerUrl,
				Filter = new FilterDescription()
				{
					Items = CreateFilters()
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
									Tip = "@[Edit]",
									Bindings = (btn) => {
										btn.SetBinding(nameof(Button.Command), EditCommand());
									}
								},
								new Button() {
									Icon=Icon.Delete,
									Tip = "@[Delete]",
									Bindings = b => {
										var bindCmd = new BindCmd() {
											Command = CommandType.DbRemoveSelected,
											Confirm = new Confirm() {Message = "@[Confirm.Delete.Element]" }
										};
										bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(arrayName));
										b.SetBinding(nameof(Button.Command), bindCmd);
									}
								},
								new Separator(),
								XamlHelper.CreateButton(CommandType.Reload, Icon.Reload, "@[Reload]"),
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
		if (filters.Any()) {
			var tp = new Taskpad();
			foreach (var filter in filters)
				if (filter.IsPeriod())
					tp.Children.Add(new PeriodPicker()
					{
						Label = filter.Name == "Date" ? "@[Period]" : filter.RealTitle(),
						Placement = DropDownPlacement.BottomRight,
						Bindings = pp => pp.SetBinding(nameof(PeriodPicker.Value), new Bind("Parent.Filter.Period"))
					});
				else
					tp.Children.Add(new SelectorSimple()
					{
						Label = filter.RealTitle(),
						Url = filter.RefUrl(),
						ShowClear = true,
						LineClamp = 2,
						Placeholder = $"@[Filter.{filter.Name}.All]",
						Bindings = ss => 
							ss.SetBinding(nameof(SelectorSimple.Value), new Bind($"Parent.Filter.{filter.Name}"))
					});
			page.Taskpad = tp;
		}
		return page;
	}

	UIElement CreateBrowseDialog(EndpointDescriptor endpoint)
	{
        var table = endpoint.BaseTable;
        var indexUi = endpoint.GetBrowseUI();
        var arrayName = endpoint.BaseTable.Name;
		var editUrl = endpoint.EndpointType() == TableType.Catalog ? $"/catalog/{table.ItemName()}/edit" :
				throw new InvalidOperationException("Invalid endpoint type");
		var dlg = new Dialog()
		{
			Title = $"@[{table.ItemName()}.Browse]",
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
							},
							ContextMenu = new DropDownMenu() {
								Children = [
									new MenuItem() {
										Content = "@[Edit]",
										Bindings = mi => {
											var bindCmd = new BindCmd() {
												Command = CommandType.Dialog,
												Action = DialogAction.EditSelected,
												Url = editUrl
											};
											bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(table.Name));
											mi.SetBinding(nameof(MenuItem.Command), bindCmd);
										}
									},
									new Separator()
								]
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
			TableCellCollection coll = [
				new TableCell()
				{
					Wrap = WrapMode.NoWrap,
					Bindings = tc => tc.SetBinding(nameof(TableCell.Content), new Bind("RowNo"))
				}
			];
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
			TableCellCollection coll = [
				new TableCell()
				{
					Wrap = WrapMode.NoWrap,
					Content = "#"
				}
			];
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

		TableCellCollection FooterCells()
		{
			TableCellCollection coll = [];
			foreach (var elem in uiElement.Fields)
			{
				if (elem.Total)
					coll.Add(new TableCell()
					{
						Align = TextAlign.Right,
						Bold = true,
						Bindings = tc =>
						{
							var bind = new Bind($"{tableName}.{uiElement.Name}.{elem.Name}")
							{
								DataType = elem.XamlDataType()
							};
							tc.SetBinding(nameof(TableCell.Content), bind);
						}
					});
				else
					coll.Add(new TableCell());
			}
			coll.Add(new TableCell()); // remove
			return coll;
		}

		TableColumnCollection TableColumns()
		{
			TableColumnCollection tc = [];
			foreach (var elem in uiElement.Fields)
				tc.Add(new TableColumn() { 
					Width = elem.XamlColumnWidth(),
					Fit = elem.Fit
				});
			tc.Add(new TableColumn() { Fit = true });
            return tc;
		}

		return new Grid(_xamlSericeProvider)
		{
			Height = Length.FromString("100%"),
			Rows = RowDefinitions.FromString("Auto,1*"),
			AlignItems = AlignItem.Stretch,
			Children = [
				new Toolbar(_xamlSericeProvider) {
					Children = [
						new Button() {
							Icon= Icon.Plus,
							Content = "@[AddRow]",
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
							Cells = FooterCells()
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

		UIElementCollection TitleFields()
		{
			UIElementCollection coll = [
				new Header() { Content = endpoint.RealName() }
			];
			var number = uiElement.Fields.FirstOrDefault(f => f.Name == "Number");
			if (number != null)
				coll.Add(new StackPanel()
				{
					Gap = GapSize.FromString(".5rem"),
					Orientation = Orientation.Horizontal,
					Children = [
						new Label() { Content = "@[Number]" },
						new TextBox() {
							Bindings = dp => dp.SetBinding(nameof(TextBox.Value), new Bind($"{table.ItemName()}.{number.Name}"))
						}
					]
				});
			var date = uiElement.Fields.FirstOrDefault(f => f.Name == "Date");
			if (date != null)
				coll.Add(new StackPanel()
				{
					Gap = GapSize.FromString(".5rem"),
					Orientation = Orientation.Horizontal,
					Children = [
						new Label() { Content = "@[Date]" },
						new DatePicker() {
							Bindings = dp => dp.SetBinding(nameof(DatePicker.Value), new Bind($"{table.ItemName()}.{date.Name}"))
						}
					]
				});
			return coll;
		}

		UIElementCollection PlainFields()
        {
            UIElementCollection coll = [];
            foreach (var f in uiElement.Fields.Where(f => f.Name != "Date" && f.Name != "Number"))
                coll.Add(f.EditField(table.ItemName()));
            return coll;
        }

		UIElementCollection DetailsBlock()
		{
			var coll = new UIElementCollection();
			if (uiElement.Details != null)
				foreach (var uiDetails in uiElement.Details)
					coll.Add(CreateEditDetails(uiDetails, table.ItemName()));
			return coll;
		}

		var page = new Page()
		{
			Title = $"{endpoint.Title}",
			Toolbar = new Toolbar(_xamlSericeProvider)
			{
				Children = [
					XamlHelper.CreateButton(CommandType.SaveAndClose, "@[SaveAndClose]", Icon.SaveCloseOutline),
					XamlHelper.CreateButton(CommandType.Save, "@[Save]", Icon.SaveOutline),
					new Separator(),
					new Button() {
						Icon = Icon.Apply,
						Content = "@[Apply]",
						Bindings = b => {
							var bindCmd = new BindCmd() {
								Command = CommandType.Execute,
								CommandName = "apply",
								SaveRequired = true,
							};
							b.SetBinding(nameof(Button.Command), bindCmd);
							b.SetBinding(nameof(Button.If), new Bind($"!{table.ItemName()}.Done"));
						}
					},
					new Button() {
						Icon = Icon.Unapply,
						Content = "@[UnApply]",
						Bindings = b => {
							var bindCmd = new BindCmd() {
								Command = CommandType.Execute,
								CommandName = "unapply"
							};
							b.SetBinding(nameof(Button.Command), bindCmd);
							b.SetBinding(nameof(Button.If), new Bind($"{table.ItemName()}.Done"));
						}
					},
					new Separator(),
					XamlHelper.CreateButton(CommandType.Reload, Icon.Reload),
					new ToolbarAligner(),
					XamlHelper.CreateButton(CommandType.Close, Icon.Close)
				]
			},
			Children = [
				new Grid(_xamlSericeProvider) {
					Columns = ColumnDefinitions.FromString("22rem,2rem,1*"),
					Rows = RowDefinitions.FromString("Auto,1*"),
					Height = Length.FromString("100%"),
					AlignItems = AlignItem.Start,
					Gap = GapSize.FromString("0"),
					Padding = Thickness.FromString("1rem"),
					Children = [
						new StackPanel() {
							Children = [
								new StackPanel() {
									Orientation = Orientation.Horizontal,
									Gap = GapSize.FromString("1rem"),
									Children = TitleFields()
								},
								new Line()
							],
							Attach = att => {
								att.Add("Grid.Row", "1");
								att.Add("Grid.ColSpan", "3");
							}

						},
						new GridDivider() {
							Attach = att => {
								att.Add("Grid.Col", "2");
								att.Add("Grid.RowSpan", "2");
							}
						},
						new Grid(_xamlSericeProvider) {
							Children = PlainFields(),
							Attach = att => {
								att.Add("Grid.Col", "1");
								att.Add("Grid.Row", "2");
							}
						},
						new Grid(_xamlSericeProvider) {
							AutoFlow = AutoFlowMode.Row,
							Height = Length.FromString("100%"),
							Children = DetailsBlock(),
							Attach = att => {
								att.Add("Grid.Col", "3");
								att.Add("Grid.Row", "2");
							}
						}
					]
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
				coll.Add(f.EditField(table.ItemName()));
			return coll;
		}

		var dlg = new Dialog()
		{
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
			],
			Bindings = dlg =>
			{
				dlg.SetBinding(nameof(Dialog.Title), new Bind($"{table.ItemName()}.Id")
				{
					Format = $"@[{table.ItemName()}] [{{0}}]"
				});
			}
		};
		return dlg;
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
		throw new InvalidOperationException("Xaml. Root is not an IXamlElement");
	}
}
