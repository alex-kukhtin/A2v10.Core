// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

using Jint.Native;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal record AccessFuncItem
{
	internal JsValue? Content;
	internal JsValue? Bullet;
	internal String? ContentExpression;
	internal String? BulletExpression;
}

internal class ListComposer : FlowElementComposer
{
	private readonly List _list;
	private readonly RenderContext _context;
	private readonly Dictionary<ListItem, AccessFuncItem> _accessFuncs = new();

	public ListComposer(List list, RenderContext context)
	{
		_list = list;
		_context = context;
	}

	internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_list))
			return;
		container
		.ApplyLayoutOptions(_list)
		.ApplyDecoration(_list.RuntimeStyle).Column(column =>
		{
			CreateAccessFunc();
			IList<ExpandoObject>? coll = null;
			if (value != null && value is IList<ExpandoObject> listColl)
				coll = listColl;
			else
			{
				var isbind = _list.GetBindRuntime("ItemsSource");
				if (isbind != null && isbind.Expression != null)
					coll = _context.Engine.EvaluateCollection(isbind.Expression);
			}

			if (coll != null)
			{
				foreach (var elem in coll)
				{
					foreach (var itm in _list.Items)
					{
						column.Item().Row(row => ComposeRow(itm, elem, row));
					}
				}
			}
			else
			{
				foreach (var itm in _list.Items)
				{
					column.Item().Row(row => ComposeRow(itm, null, row));
				}
			}
			/*
			foreach (var i in Enumerable.Range(1, 8))
			{
				column.Item().Row(row =>
				{
					if (_list.Spacing != 0)
						row.Spacing(_list.Spacing);
					row.AutoItem().Text($"{i}."); // text or image
					row.RelativeItem().Text(Placeholders.Sentence());
				});
			}
			*/
		});
	}

	private void CreateAccessFunc()
	{
		foreach (var item in _list.Items)
		{
			var cont = item.GetBindRuntime("Content");
			JsValue? contFunc = null;
			JsValue? bulletFunc = null;
			if (cont != null && cont.Expression != null)
				contFunc = _context.Engine.CreateAccessFunction(cont.Expression);
			var bull = item.GetBindRuntime("Bullet");
			if (bull != null && bull.Expression != null)
				bulletFunc = _context.Engine.CreateAccessFunction(bull.Expression);
			if (contFunc != null || bulletFunc != null)
				_accessFuncs.Add(item, new AccessFuncItem()
				{
					Content = contFunc, Bullet = bulletFunc,
					ContentExpression = cont?.Expression, BulletExpression = bull?.Expression
				});
		}
	}

	void ComposeBullet(ListItem item, ExpandoObject? elem, RowDescriptor row)
	{
		if (_accessFuncs.TryGetValue(item, out var accessFunc))
		{
			if (accessFunc.Bullet != null)
			{
				var bullet = _context.Engine.Invoke(accessFunc.Bullet, elem, accessFunc.BulletExpression);
				row.AutoItem().Text(_context.ValueToString(bullet));
				return;
			}
		}
		if (item.Bullet != null)
			row.AutoItem().Text(item.Bullet.ToString());
	}

	void ComposeRow(ListItem item, ExpandoObject? elem, RowDescriptor row)
	{
		if (_list.Spacing != 0)
			row.Spacing(_list.Spacing);
		ComposeBullet(item, elem, row);
		if (_accessFuncs.TryGetValue(item, out var accessFunc))
		{
			if (accessFunc.Content != null)
			{
				var value = _context.Engine.Invoke(accessFunc.Content, elem, accessFunc.ContentExpression);
				if (value != null)
					row.RelativeItem().Text(_context.ValueToString(value))
						.ApplyText(item.RuntimeStyle);
			}
		}
		else if (item.Content is FlowElement flowElem)
		{
			flowElem.CreateComposer(_context).Compose(row.RelativeItem());
		}
		else
		{
			var val = _context.GetValueAsString(item);
			if (val != null)
			{
				row.RelativeItem().Text(val).ApplyText(item.RuntimeStyle);
			}
		}
	}
}
