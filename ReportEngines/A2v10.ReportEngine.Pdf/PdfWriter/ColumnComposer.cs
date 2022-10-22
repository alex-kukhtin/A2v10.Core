// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class ColumnComposer : FlowElementComposer
{
	private readonly Column _column;
	private readonly RenderContext _context;

	internal ColumnComposer(Column column, RenderContext context)
	{
		_column = column;
		_context = context;
	}

	internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_column))
			return;
		container
			.ApplyLayoutOptions(_column)
			.ApplyDecoration(_column.RuntimeStyle).Column(Compose);
	}

	public void Compose(ColumnDescriptor column)
	{
		foreach (var ch in _column.Children)
		{
			column.Item().Element(cont =>
			{
				ComposeElement(cont, ch);
			});
		}
	}

	void ComposeElement(IContainer container, FlowElement elem)
	{
		var comp = elem.CreateComposer(_context);
		comp.Compose(container);
	}
}
