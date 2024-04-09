// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.System.Xaml;
using A2v10.Xaml.DynamicRendrer;
using A2v10.Xaml;

namespace A2v10.AppRuntimeBuilder;

internal class ModelPageBuilder(IServiceProvider _serviceProvider)
{
	private readonly IServiceProvider _xamlSericeProvider = new XamlServiceProvider();
	private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
	private readonly IAppCodeProvider _codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
	private readonly IXamlPartProvider _xamlPartProvider = _serviceProvider.GetRequiredService<IXamlPartProvider>();

	const Int32 COLUMN_MAX_CHARS = 50;
	public async Task<String> RenderPageAsync(IPlatformUrl platformUrl, IModelView modelView, RuntimeTable table, IDataModel dataModel)
	{
		String rootId = $"el{Guid.NewGuid()}";

		String templateText = String.Empty;
		if (!String.IsNullOrEmpty(modelView.Template))
			templateText = await GetTemplateScriptAsync(modelView);
		else
		{
			if (modelView.IsIndex)
				templateText = CreateIndexTemplate(table);
			else if (platformUrl.Action == "edit")
				templateText = CreateEditTemplate(modelView);
		}

		UIElement? page = null;

		var rawView = modelView.GetRawView(false);
		if (!String.IsNullOrEmpty(rawView))
			page = LoadPage(modelView, rawView);
		else if (modelView.IsIndex && !modelView.IsDialog)
			page = CreateIndexPage(platformUrl, table);
		else if (!modelView.IsIndex && modelView.IsDialog && platformUrl.Action == "edit")
			page = CreateEditDialog(table);
		else if (modelView.IsIndex && modelView.IsDialog && platformUrl.Action == "browse")
			throw new InvalidOperationException("Generate browse dialog");

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

	UIElement CreateIndexPage(IPlatformUrl platformUrl, RuntimeTable table)
	{
		DataGridColumnCollection CreateColumns()
		{
			DataGridColumnCollection columns = [
				new DataGridColumn() {
					Header = "#",
					Role = ColumnRole.Id,
					Bindings = (c) => {
						c.SetBinding(nameof(DataGridColumn.Content), new Bind("Id"));
					}
				},
				new DataGridColumn() {
					Header = "@[Name]",
					MaxChars = COLUMN_MAX_CHARS,
					Bindings = (c) => {
						c.SetBinding(nameof(DataGridColumn.Content), new Bind("Name"));
					}
				},
			];
			table.Fields.ForEach(f =>
			{
				columns.Add(new DataGridColumn()
				{
					Header = $"@[{f.Name}]",
					MaxChars = f.HasMaxChars() ? COLUMN_MAX_CHARS : 0,
					Bindings = c => {
						c.SetBinding(nameof(DataGridColumn.Content), new Bind(f.Name));
					}
				}); 
			});
			columns.Add(
				new DataGridColumn()
				{
					Header = "@[Memo]",
					MaxChars =	COLUMN_MAX_CHARS,
					Bindings = (c) =>
					{
						c.SetBinding(nameof(DataGridColumn.Content), new Bind("Memo"));
					}
				}
			);

			return columns;
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
					cw.SetBinding(nameof(CollectionView.ItemsSource), new Bind(table.Name));
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
										var bindCmd = new BindCmd() {
											Command = Xaml.CommandType.Dialog,
											Action = DialogAction.Append,
											Url = $"/{platformUrl.LocalPath}/edit",
										};
										bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(table.Name));
										btn.SetBinding(nameof(Button.Command), bindCmd);
									}
								},
								new Button() {
									Icon=Icon.Edit,
									Bindings = (btn) => {
										var bindCmd = new BindCmd() {
											Command = Xaml.CommandType.Dialog,
											Action = DialogAction.EditSelected,
											Url = $"/{platformUrl.LocalPath}/edit",
										};
										bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(table.Name));
										btn.SetBinding(nameof(Button.Command), bindCmd);
									}
								},
								new Separator(),
								new Button() {
									Content = "@[Reload]",
									Icon = Icon.Reload,
									Bindings = (btn) => {
										btn.SetBinding(nameof(Button.Command), new BindCmd() {Command = Xaml.CommandType.Reload});
									}
								},
								new ToolbarAligner(),
								new TextBox() {
									Placeholder = "@[Search]",
									Width = Length.FromString("20rem"),
									Bindings = tb => {
										tb.SetBinding(nameof(TextBox.Value), new Bind("Parent.Filter.Fragment"));
									}
								}
							]
						},
						new DataGrid() {
							FixedHeader = true,
							Sort = true,
							Bindings = (dg) => {
								dg.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
							},
							Columns = CreateColumns()
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

	UIElement CreateEditDialog(RuntimeTable table)
	{
		UIElementCollection CreateDialogChildren()
		{
			UIElementCollection coll = [
				new TextBox()
				{
					Label = "@[Name]",
					Bold = true,
					TabIndex = 1,
					Bindings = (txt) => {
						txt.SetBinding(nameof(TextBox.Value), new Bind($"{table.ItemName}.Name"));
					}
			}];
			foreach (var f in table.Fields)
			{
				coll.Add(String.IsNullOrEmpty(f.Ref) ?
					new TextBox()
					{
						Label = $"$[{f.Name}]",
						Multiline = f.IsMultiline(),
						Bindings = (txt) =>
						{
							txt.SetBinding(nameof(TextBox.Value), new Bind($"{table.ItemName}.{f.Name}"));
						}
					} :
					new SelectorSimple()
					{
						Label = $"$[{f.Name}]",
						Url = f.RefUrl(),
						Bindings = ss =>
						{
							ss.SetBinding(nameof(SelectorSimple.Value), new Bind($"{table.ItemName}.{f.Name}"));
						}
					}
				);
			}
			coll.Add(new TextBox()
			{
				Label = "@[Memo]",
				Multiline = true,
				Bindings = (txt) =>
				{
					txt.SetBinding(nameof(TextBox.Value), new Bind($"{table.ItemName}.Memo"));
				}
			});
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
				new Button() {Content = "@[Cancel]",
					Bindings = (btn) => {
						btn.SetBinding(nameof(Button.Command), new BindCmd("Close"));
					}
				}
			],
			Children = [
				new Grid(_xamlSericeProvider) {
					Children = CreateDialogChildren()
				}
			]
		};
		return dlg;
	}

	String CreateIndexTemplate(RuntimeTable table)
	{
		var template = $$"""

			const template = {
				options:{
					noDirty: true,
					persistSelect: ['{{table.Name}}']
				},
				validators: {
				}
			};

			module.exports = template;            
			""";
		return template;
	}

	String CreateEditTemplate(IModelView modelView)
	{
		var template = $$"""
			const template = {
				validators: {
					'Agent.Name' : '@[Error.Required]'
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
