// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.Xaml.Auto;

[ContentProperty("Columns")]
public class IndexPage : UIElement, IRootContainer, IUriContext, ISupportPlatformUrl
{
	public String Source { get; set; } = String.Empty;

	public DataColumnCollection Columns { get; set; } = [];
	public Uri? BaseUri { get; set; }

	private IPlatformUrl? PlatformUrl { get; set; }

	private readonly IServiceProvider _xamlSericeProvider = new XamlServiceProvider();
	private UIElement? _page;

	public void SetPlatformUrl(IPlatformUrl platformUrl)
	{
		PlatformUrl = platformUrl;
		_page = Build();
	}
	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		_page?.RenderElement(context, onRender);
	}

	private UIElement Build()
	{
        if (_page != null)
			return _page;

        DataGridColumnCollection GetColumns()
		{
			DataGridColumnCollection columns = [];
			foreach (var ic in Columns)
				columns.Add(ic.DataGridColumn);
			return columns;
		}

		Toolbar CreateToolbar()
		{
			var tb = new Toolbar(_xamlSericeProvider);
			if (PlatformUrl != null) {
				tb.Children.Add(new Button()
				{
					Icon = Icon.Edit,
					Bindings = btn => {
						var bindCmd = new BindCmd()
						{
							Command = Xaml.CommandType.Dialog,
							Action = DialogAction.EditSelected,
							Url = $"/{PlatformUrl.LocalPath}/edit",
						};
						bindCmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(Source));
						btn.SetBinding(nameof(Button.Command), bindCmd);
					}
				});
				tb.Children.Add(new Separator());
			}
			tb.Children.Add(
				new Button() {
					Icon = Icon.Reload,
					Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd("Reload"))
				});
			tb.Children.Add(new ToolbarAligner());
			tb.Children.Add(new TextBox() {
				Placeholder = "@[Search]",
				Width = Length.FromString("20rem"),
				Bindings = tb => tb.SetBinding(nameof(TextBox.Value), new Bind("Parent.Filter.Fragment"))
			});
				
			return tb;
		}

		var page = new Page()
		{
			CollectionView = new()
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
				Bindings = c =>
					c.SetBinding(nameof(CollectionView.ItemsSource), new Bind(Source))
			},
			Children = [
			new Grid(_xamlSericeProvider)
			{
				Height = Length.FromString("100%"),
				Rows = [
					new RowDefinition() {Height = GridLength.FromString("Auto")},
					new RowDefinition() {Height = GridLength.FromString("1*")},
					new RowDefinition() {Height = GridLength.FromString("Auto")},
				],
				Children = [
					CreateToolbar(),
					new DataGrid() {
						Columns = GetColumns(),
						Sort = true,
						FixedHeader = true,	
						Bindings = dg => {
							dg.SetBinding(nameof(DataGrid.ItemsSource), new Bind("Parent.ItemsSource"));
						}
					},
					new Pager() {
						Bindings = p => {
							p.SetBinding(nameof(Pager.Source), new Bind("Parent.Pager"));
						}
					}
				]
			}
		]};
		if (page is IInitComplete initComplete)
			initComplete.InitComplete();
		return page;
	}

	public void SetStyles(Styles styles)
	{
		if (_page is IRootContainer container)
			container.SetStyles(styles);
	}

	public XamlElement? FindComponent(string name)
	{
        if (_page is IRootContainer container)
			return container.FindComponent(name);
		return null;
	}
}
