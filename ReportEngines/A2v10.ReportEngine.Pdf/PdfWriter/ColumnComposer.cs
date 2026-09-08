// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class ColumnComposer(Column _column, RenderContext _context) : FlowElementComposer
{
	internal override void Compose(IContainer container, ExpandoObject scope)
	{
		if (!_context.IsVisible(_column, scope))
			return;
		container
			.ApplyLayoutOptions(_column)
			.ApplyDecoration(_column.RuntimeStyle).Column(column => Compose(column, scope));
	}

	public void Compose(ColumnDescriptor column, ExpandoObject scope)
	{
		foreach (var ch in _column.Children)
		{
			column.Item().Element(cont =>
			{
				ComposeElement(cont, ch, scope);
			});
		}
	}

	void ComposeElement(IContainer container, FlowElement elem, ExpandoObject scope)
	{
		var comp = elem.CreateComposer(_context);
		comp.Compose(container, scope);
	}
}
