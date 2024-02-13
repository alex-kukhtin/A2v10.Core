// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class PageComposer
{
	private readonly Page _page;
	private readonly RenderContext _context;

	internal PageComposer(A2v10.Xaml.Report.Page report, RenderContext context)
	{
		_page = report;
		_context = context;
	}

	internal void Compose(PageDescriptor page)
	{
		// TODO: styles

		var size = PageSizes.A4;
		switch (_page.Orientation)
		{
			case PageOrientation.Portrait:
				size = size.Portrait();
				break;
			case PageOrientation.Landscape:
				size = size.Landscape();
				break;
		}
		page.Size(size);
		var rs = _page.GetRuntimeStyle();
		if (rs != null && rs.Margin != null)
		{
			var mrg = rs.Margin;
			page.MarginLeft(mrg.Left.Value, mrg.Left.Unit.ToUnit());
			page.MarginRight(mrg.Right.Value, mrg.Right.Unit.ToUnit());
			page.MarginTop(mrg.Top.Value, mrg.Top.Unit.ToUnit());
			page.MarginBottom(mrg.Bottom.Value, mrg.Bottom.Unit.ToUnit());
		}

		page.DefaultTextStyle(ts => {
			if (!String.IsNullOrEmpty(_page.FontFamily))
				ts = ts.FontFamily(_page.FontFamily!);
			else
				ts = ts.FontFamily(Fonts.Verdana);
			if (rs != null && rs.FontSize != null)
				ts = ts.FontSize(rs.FontSize.Value);
			else
				ts = ts.FontSize(9.75F);
			return ts;
		});

		// header
		if (_page.Header != null)
			page.Header().Element(ComposeHeader);

		// content
		page.Content().Element(ComposeContent);

		if (_page.Footer != null)
			page.Footer().Element(ComposeFooter);
	}

	void ComposeHeader(IContainer container)
	{
		if (_page.Header == null)
			return;
		container.Column(column =>
		{
			var cc = new ColumnComposer(_page.Header, _context);
			cc.Compose(column);
		});
	}

	void ComposeContent(IContainer container)
	{
		if (_page is Spreadsheet spreadsheet)
		{
			container.Column(column =>
			{
				var cc = new WorkbookComposer(spreadsheet.Workbook, _context);
				cc.Compose(column);
			});
		}
		else if (_page.Columns.Count > 0)
		{
			foreach (var c in _page.Columns)
			{
				container.Column(column =>
				{
					var cc = new ColumnComposer(c, _context);
					cc.Compose(column);
				});
			}
		}
	}

	void ComposeFooter(IContainer container)
	{
		if (_page.Footer == null)
			return;
		container.Column(column =>
		{
			var cc = new ColumnComposer(_page.Footer, _context);
			cc.Compose(column);
		});
	}
}
