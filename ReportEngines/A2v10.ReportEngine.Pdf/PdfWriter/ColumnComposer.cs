// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class ColumnComposer
{
	private readonly Column _column;
	private readonly RenderContext _context;

	internal ColumnComposer(Column column, RenderContext context)
	{
		_column = column;
		_context = context;
	}

	public void Compose(ColumnDescriptor column)
	{
		foreach (var ch in _column.Children)
		{
			/*
			column.Item().Padding(10F).Text(text =>
			{
				//text.DefaultTextStyle(TextStyle.Default.FontSize(16F));
				text.Hyperlink("https://www.google.com/", "https://www.google.com/").Underline().FontColor(Colors.Indigo.Accent4);
				text.Span("Span 1").Bold();
				//text.Span(" ");
				text.Span("Span 2").Italic();
				//text.Span(" ");
				text.Span("Span 3").Underline();
				//text.Span(" ");
			});
			column.Item().PaddingVertical(5).LineHorizontal(.1F).LineColor(Colors.Grey.Medium);
			column.Item().Padding(10F).Hyperlink("https:/google.com").Text("Google!");
			*/

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
