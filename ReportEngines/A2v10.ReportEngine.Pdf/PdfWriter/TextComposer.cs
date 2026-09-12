// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class TextComposer(Text text, RenderContext context) : FlowElementComposer
{
	private readonly Text _text = text;
	private readonly RenderContext _context = context;

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
		if (rs.Align == TextAlign.Justify)
			descr.Justify();
	}

	static TextSpanDescriptor? ApplyRuntimeStyle(TextSpanDescriptor? descr, ContentElement elem)
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

	// Значение композера, а не модели: номера страницы в данных нет и быть не может — отсюда
	// и свой знак, $() против {}. Неизвестное имя валит бланк: подстановка «ничего» съедала бы
	// опечатку вместе со всей строкой, и промах был бы неотличим от пустого значения
	static TextSpanDescriptor Macro(TextDescriptor txt, String name)
	{
		return name switch
		{
			"PageNumber" => txt.CurrentPageNumber(),
			"TotalPages" => txt.TotalPages(),
			_ => throw new XamlException($"Unknown macro '$({name})'")
		};
	}

	// Литерал режется по $(...): текст идёт спанами, макрос — своим дескриптором. Стиль
	// применяется к каждому куску — элемент разметки один, и делиться стиль не должен.
	// Незакрытая скобка макросом не является: '$(' в обычном тексте остаётся текстом
	static void ComposeMacroText(TextDescriptor txt, String text, ContentElement elem)
	{
		var pos = 0;
		while (pos < text.Length)
		{
			var start = text.IndexOf("$(", pos, StringComparison.Ordinal);
			if (start < 0)
				break;
			var end = text.IndexOf(')', start + 2);
			if (end < 0)
				break;
			if (start > pos)
				ApplyRuntimeStyle(txt.Span(text[pos..start]), elem);
			ApplyRuntimeStyle(Macro(txt, text[(start + 2)..end]), elem);
			pos = end + 1;
		}
		if (pos < text.Length)
			ApplyRuntimeStyle(txt.Span(text[pos..]), elem);
	}

	internal override void Compose(IContainer container, ExpandoObject scope)
	{
		if (!_context.IsVisible(_text, scope))
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
				// Перенос — разметка, а не текст, поэтому здесь, рядом с пробелом: общий путь
				// обрезает значение спана Trim'ом, и перенос символом через него не проходит
				if (elem is Break)
				{
					txt.Line(String.Empty);
					continue;
				}
				var val = _context.GetValueAsString(elem, scope);
				if (val != null)
				{
					var txtVal = val.TrimForSpan();
					if (i != _text.Inlines.Count - 1)
						txtVal += " ";
					var contElem = elem as ContentElement;
					// Макрос ищется только в литерале. Связанное значение — это данные, и разметкой
					// они не становятся: иначе строка из базы начала бы печатать номер страницы
					if (contElem != null && contElem.GetBindRuntime("Content") == null)
						ComposeMacroText(txt, txtVal, contElem);
					else if (contElem != null)
						ApplyRuntimeStyle(txt.Span(txtVal), contElem);
					else
						txt.Span(txtVal);
				}
			}
		});
	}
}
