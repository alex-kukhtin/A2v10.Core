// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class LineComposer(Line line, RenderContext context) : FlowElementComposer
{
	private readonly Line _line = line;
	private readonly RenderContext _context = context;

    internal override void Compose(IContainer container, ExpandoObject scope)
	{
		if (!_context.IsVisible(_line, scope))
			return;
		container.ApplyDecoration(_line.RuntimeStyle).LineHorizontal(_line.Thickness.Value, _line.Thickness.Unit.ToUnit());
	}
}
