// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml.Report;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace A2v10.ReportEngine.Pdf;

using Image = A2v10.Xaml.Report.Image;

internal static class Extensions
{
	public static FlowElementComposer CreateComposer(this FlowElement elem, RenderContext context)
	{
		return elem switch
		{
			Column col => new ColumnComposer(col, context),
			Table table => new TableComposer(table, context),
			Text text => new TextComposer(text, context),
			Image image => new ImageComposer(image, context),
			Line line => new LineComposer(line),
			List list => new ListComposer(list, context),
			Inlined inlined => new InlinedComposer(inlined, context),
			Checkbox checkbox => new CheckboxComposer(checkbox, context),
			_ => throw new InvalidOperationException($"There is no composer for {elem.GetType()}")
		};
	}

	public static void TableColumn(this TableColumnsDefinitionDescriptor desc, TableColumn column)
	{
		if (column.Width?.Unit == "fr")
			desc.RelativeColumn(column.Width.Value);
		else if (column.Width != null)
			desc.ConstantColumn(column.Width.Value, GetUnit(column.Width.Unit));
		else
			desc.RelativeColumn(1);
	}

	public static Unit ToUnit(this String unit)
	{
		return GetUnit(unit);
	}

	public static String TrimForSpan(this String text)
	{
		return text.Trim();
	}

	public static Unit GetUnit(String ext)
	{
		return ext switch
		{
			"pt" => Unit.Point,
			"mm" => Unit.Millimetre,
			"cm" => Unit.Centimetre,
			"in" => Unit.Inch,
			_ => throw new ArgumentOutOfRangeException($"Invalid unit '{ext}'")
		};
	}
}
