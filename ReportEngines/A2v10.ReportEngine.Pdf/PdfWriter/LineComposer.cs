// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class LineComposer : FlowElementComposer
{
	private readonly Line _line;
	public LineComposer(Line line)
	{
		_line = line;
	}

	internal override void Compose(IContainer container, Object? value = null	)
	{
		container.ApplyDecoration(_line.RuntimeStyle).LineHorizontal(_line.Thickness.Value, _line.Thickness.Unit.ToUnit());
	}
}
