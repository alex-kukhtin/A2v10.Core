// Copyright © 2022-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class ListComposer(List list, RenderContext context) : FlowElementComposer
{
	private readonly List _list = list;
	private readonly RenderContext _context = context;

    internal override void Compose(IContainer container, ExpandoObject scope)
	{
		if (!_context.IsVisible(_list, scope))
			return;
		container
		.ApplyLayoutOptions(_list)
		.ApplyDecoration(_list.RuntimeStyle).Column(column =>
		{
			var isbind = _list.GetBindRuntime("ItemsSource");
			var coll = isbind?.Expression != null
				? _context.EvaluateCollection(isbind.Expression, scope)
				: null;

			if (coll != null)
			{
				foreach (var elem in coll)
					foreach (var itm in _list.Items)
						column.Item().Row(row => ComposeRow(itm, elem, row));
			}
			else
			{
				foreach (var itm in _list.Items)
					column.Item().Row(row => ComposeRow(itm, scope, row));
			}
		});
	}

	void ComposeBullet(ListItem item, ExpandoObject scope, RowDescriptor row)
	{
		var bind = item.GetBindRuntime("Bullet");
		if (bind != null && bind.Expression != null)
		{
			var bullet = _context.Evaluate(bind.Expression, scope);
			row.AutoItem().Text(_context.ValueToString(bullet));
		}
		else if (item.Bullet != null)
			row.AutoItem().Text(item.Bullet.ToString());
	}

	void ComposeRow(ListItem item, ExpandoObject scope, RowDescriptor row)
	{
		if (_list.Spacing != 0)
			row.Spacing(_list.Spacing);
		ComposeBullet(item, scope, row);

		var bind = item.GetBindRuntime("Content");
		if (bind != null && bind.Expression != null)
		{
			var value = _context.Evaluate(bind.Expression, scope);
			if (value != null)
				row.RelativeItem().Text(_context.ValueToString(value))
					.ApplyText(item.RuntimeStyle);
		}
		else if (item.Content is FlowElement flowElem)
			flowElem.CreateComposer(_context).Compose(row.RelativeItem(), scope);
		else
		{
			var val = _context.GetValueAsString(item, scope);
			if (val != null)
				row.RelativeItem().Text(val).ApplyText(item.RuntimeStyle);
		}
	}
}
