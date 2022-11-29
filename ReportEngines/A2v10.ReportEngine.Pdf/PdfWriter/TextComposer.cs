// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class TextComposer : FlowElementComposer
{
	private readonly Text _text;
	private readonly RenderContext _context;

	public TextComposer(Text text, RenderContext context)
	{
		_text = text;
		_context = context;
	}

	void ApplyRuntimeStyle(TextDescriptor descr)
	{
		var rs = _text.RuntimeStyle;
		if (rs == null)
			return;
		var ts = QuestPDF.Infrastructure.TextStyle.Default;
		if (rs.FontSize != null)
			ts = ts.FontSize(rs.FontSize.Value);
		if (rs.Bold != null && rs.Bold.Value)
			ts = ts.Bold();
		if (rs.Italic != null && rs.Italic.Value)
			ts = ts.Italic();
		if (rs.Underline != null && rs.Underline.Value)
			ts = ts.Underline();
		if (!String.IsNullOrEmpty(rs.Color))
			ts = ts.FontColor(rs.Color!);
		descr.DefaultTextStyle(ts);
	}

	TextSpanDescriptor? ApplyRuntimeStyle(TextSpanDescriptor? descr, ContentElement elem)
	{
		if (descr == null)
			return descr;
		var rs = elem.RuntimeStyle;
		if (rs == null)
			return descr;
		if (rs.FontSize != null)
			descr = descr.FontSize(rs.FontSize.Value);
		if (rs.Bold != null && rs.Bold.Value)
			descr = descr.Bold();
		if (rs.Italic != null && rs.Italic.Value)
			descr = descr.Italic();
		if (rs.Underline != null && rs.Underline.Value)
			descr = descr.Underline();
		if (!String.IsNullOrEmpty(rs.Color))
			descr = descr.FontColor(rs.Color!);
		if (!String.IsNullOrEmpty(rs.Background))
			descr = descr.BackgroundColor(rs.Background!);
		return descr;
	}

	internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_text))
			return;
		container
		.ApplyLayoutOptions(_text)
		.ApplyDecoration(_text.RuntimeStyle)
		.Text(txt =>
		{
			//_context.ApplyTextStyle(txt, _text.Style);
			ApplyRuntimeStyle(txt);
			//txt.DefaultTextStyle(TextStyle.Default.FontSize(16F));
			for (var i = 0; i < _text.Inlines.Count; i++)
			{
				var elem = _text.Inlines[i];
				if (elem is Space elemSpace)
				{
					if (elemSpace.Width != null)
						txt.Element().MinWidth(elemSpace.Width.Value, elemSpace.Width.Unit.ToUnit());
					continue;
				}
				var val = _context.GetValueAsString(elem);
				if (val != null)
				{
					var txtVal = val.TrimForSpan();
					if (i != _text.Inlines.Count - 1)
						txtVal += " ";
					TextSpanDescriptor? res = null;
					if (val.StartsWith("$("))
					{
						switch (val)
						{
							case "$(PageNumber)":
								res = txt.CurrentPageNumber();
								break;
							case "$(TotalPages)":
								res = txt.TotalPages();
								break;
						}
					}
					else
						res = txt.Span(txtVal);
					if (elem is ContentElement contElem)
					{
						ApplyRuntimeStyle(res, contElem);
					}
				}
			}
		});
	}
}
