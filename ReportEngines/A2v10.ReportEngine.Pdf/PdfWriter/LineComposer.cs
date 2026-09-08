// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class LineComposer(Line line) : FlowElementComposer
{
	private readonly Line _line = line;

    internal override void Compose(IContainer container, ExpandoObject scope)
	{
		container.ApplyDecoration(_line.RuntimeStyle).LineHorizontal(_line.Thickness.Value, _line.Thickness.Unit.ToUnit());
	}
}
