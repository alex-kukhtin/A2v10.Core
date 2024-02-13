// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class ColumnComposer(Column _column, RenderContext _context) : FlowElementComposer
{
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
