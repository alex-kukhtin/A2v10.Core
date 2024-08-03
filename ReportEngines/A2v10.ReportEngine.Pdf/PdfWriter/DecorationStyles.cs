// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Elements.Table;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal static class DecorationStyles
{

	public static IContainer ApplyPadding(this IContainer container, Thickness? padding)
	{
		if (padding == null)
			return container;
		var pad = padding.All();
		if (pad != null && !pad.IsEmpty())
			return container.Padding(pad.Value, Extensions.GetUnit(pad.Unit));

		if (!padding.Top.IsEmpty())
			container = container.PaddingTop(padding.Top.Value, Extensions.GetUnit(padding.Top.Unit));
		if (!padding.Right.IsEmpty())
			container = container.PaddingRight(padding.Right.Value, Extensions.GetUnit(padding.Right.Unit));
		if (!padding.Bottom.IsEmpty())
			container = container.PaddingBottom(padding.Bottom.Value, Extensions.GetUnit(padding.Bottom.Unit));
		if (!padding.Left.IsEmpty())
			container = container.PaddingLeft(padding.Left.Value, Extensions.GetUnit(padding.Left.Unit));
		return container;
	}

	public static IContainer ApplyBorder(this IContainer container, Thickness? border)
	{
		if (border == null)
			return container;
		var pad = border.All();
		if (pad != null && !pad.IsEmpty())
			return container.Border(pad.Value, Extensions.GetUnit(pad.Unit));

		if (!border.Top.IsEmpty())
			container = container.BorderTop(border.Top.Value, border.Top.Unit.ToUnit());
		if (!border.Right.IsEmpty())
			container = container.BorderRight(border.Right.Value, border.Right.Unit.ToUnit());
		if (!border.Bottom.IsEmpty())
			container = container.BorderBottom(border.Bottom.Value, border.Bottom.Unit.ToUnit());
		if (!border.Left.IsEmpty())
			container = container.BorderLeft(border.Left.Value, border.Left.Unit.ToUnit());
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
		if (style.TextRotation == 90)
			container = container.RotateLeft();
		else if (style.TextRotation == 180)
			container = container.RotateRight();
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

	public static TextBlockDescriptor ApplyText(this TextBlockDescriptor container, RuntimeStyle? style)
	{
		if (style == null)
			return container;
		if (style.Align == TextAlign.Justify)
			container.Justify();
		if (!String.IsNullOrEmpty(style.FontName))
			container = container.FontFamily(style.FontName);	
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
