// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Elements.Table;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal static class DecorationStyles
{

	public static IContainer ApplyPadding(this IContainer container, Thickness? thickness)
	{
		if (thickness == null)
			return container;
		var pad = thickness.All();
		if (pad != null && !pad.IsEmpty())
			return container.Padding(pad.Value, Extensions.GetUnit(pad.Unit));

		if (!thickness.Top.IsEmpty())
			container = container.PaddingTop(thickness.Top.Value, Extensions.GetUnit(thickness.Top.Unit));
		if (!thickness.Right.IsEmpty())
			container = container.PaddingRight(thickness.Right.Value, Extensions.GetUnit(thickness.Right.Unit));
		if (!thickness.Bottom.IsEmpty())
			container = container.PaddingBottom(thickness.Bottom.Value, Extensions.GetUnit(thickness.Bottom.Unit));
		if (!thickness.Left.IsEmpty())
			container = container.PaddingLeft(thickness.Left.Value, Extensions.GetUnit(thickness.Left.Unit));
		return container;
	}

	public static IContainer ApplyBorder(this IContainer container, Thickness? thickness)
	{
		if (thickness == null)
			return container;
		var pad = thickness.All();
		if (pad != null && !pad.IsEmpty())
			return container.Border(pad.Value, Extensions.GetUnit(pad.Unit));

		if (!thickness.Top.IsEmpty())
			container = container.BorderTop(thickness.Top.Value, thickness.Top.Unit.ToUnit());
		if (!thickness.Right.IsEmpty())
			container = container.BorderRight(thickness.Right.Value, thickness.Right.Unit.ToUnit());
		if (!thickness.Bottom.IsEmpty())
			container = container.BorderBottom(thickness.Bottom.Value, thickness.Bottom.Unit.ToUnit());
		if (!thickness.Left.IsEmpty())
			container = container.BorderLeft(thickness.Left.Value, thickness.Left.Unit.ToUnit());
		return container;
	}

	public static IContainer ApplyAlign(this IContainer container, RuntimeStyle? style)
	{
		if (style == null)
			return container;
		if (style.Align != null)
		{
			switch (style.Align.Value)
			{
				case TextAlign.Left:
					container = container.AlignLeft();
					break;
				case TextAlign.Center:
					container = container.AlignCenter();
					break;
				case TextAlign.Right:
					container = container.AlignRight();
					break;
			}
		}
		if (style.VAlign != null)
		{
			switch (style.VAlign.Value)
			{
				case VertAlign.Top:
					container = container.AlignTop();
					break;
				case VertAlign.Middle:
					container = container.AlignMiddle();
					break;
				case VertAlign.Bottom:
					container = container.AlignBottom();
					break;
			}
		}
		return container;
	}
	public static IContainer ApplyDecoration(this IContainer container, RuntimeStyle? style)
	{
		if (style == null)
			return container;
		container = container.ApplyPadding(style.Margin);
		if (style.Background != null)
			container = container.Background(style.Background);
		if (style.Border != null)
			container = container.ApplyBorder(style.Border);
		container = container.ApplyPadding(style.Padding).ApplyAlign(style);
		return container;
	}

	public static IContainer ApplyLayoutOptions(this IContainer container, XamlElement elem)
	{
		if (elem.ShowEntire)
			return container.ShowEntire();
		return container;
	}

	public static IContainer ApplyCellDecoration(this ITableCellContainer container, RuntimeStyle? style)
	{
		if (style == null)
			return container;
		return container.ApplyDecoration(style);
	}

	public static TextSpanDescriptor ApplyText(this TextSpanDescriptor container, RuntimeStyle? style)
	{
		if (style == null)
			return container;
		if (style.FontSize != null)
			container = container.FontSize(style.FontSize.Value);
		if (style.Bold != null && style.Bold.Value)
			container = container.Bold();
		if (style.Italic != null && style.Italic.Value)
			container = container.Italic();
		if (style.Underline != null && style.Underline.Value)
			container = container.Underline();
		if (!String.IsNullOrEmpty(style.Color))
			container = container.FontColor(style.Color!);
		return container;
	}
}
