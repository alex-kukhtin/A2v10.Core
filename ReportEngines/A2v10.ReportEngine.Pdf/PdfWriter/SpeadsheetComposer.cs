// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Helpers;

using A2v10.Xaml.Report;
using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;
using A2v10.ReportEngine.Excel;

namespace A2v10.ReportEngine.Pdf;

internal class SpreadsheetComposer
{
	private readonly Spreadsheet _ssheet;
	private readonly RenderContext _context;

	internal SpreadsheetComposer(Spreadsheet report, RenderContext context)
	{
		_ssheet = report;
		_context = context;
	}

	internal void Compose(PageDescriptor page)
	{
		// TODO: styles

		var size = PageSizes.A4;
		switch (_ssheet.Orientation)
		{
			case PageOrientation.Portrait:
				size = size.Portrait();
				break;
			case PageOrientation.Landscape:
				size = size.Landscape();
				break;
		}
		page.Size(size);
		var rs = _ssheet.GetRuntimeStyle();
		if (rs != null && rs.Margin != null)
		{
			var mrg = rs.Margin;
			page.MarginLeft(mrg.Left.Value, mrg.Left.Unit.ToUnit());
			page.MarginRight(mrg.Right.Value, mrg.Right.Unit.ToUnit());
			page.MarginTop(mrg.Top.Value, mrg.Top.Unit.ToUnit());
			page.MarginBottom(mrg.Bottom.Value, mrg.Bottom.Unit.ToUnit());
		}

		page.DefaultTextStyle(ts => {
			if (!String.IsNullOrEmpty(_ssheet.FontFamily))
				ts = ts.FontFamily(_ssheet.FontFamily!);
			else
				ts = ts.FontFamily(Fonts.Calibri);
			if (rs != null && rs.FontSize != null)
				ts = ts.FontSize(rs.FontSize.Value);
			else if (_ssheet.FontSize != null)
				ts = ts.FontSize(_ssheet.FontSize.Value);
			else
				ts = ts.FontSize(9.75F);
			return ts;
		});

		var wbHelper = new WorkbookHelper(_ssheet.Workbook, _context);
		var wbComposer = new WorkbookComposer(_ssheet.Workbook, wbHelper, _context);
		if (_ssheet.Workbook.Header != null)
		{
			page.Header().Element(container =>
			{
				container.Column(col =>
				{
					wbComposer.ComposeHeader(col);
				});
			});
		}
		// content
		page.Content().Element(container =>
		{
			container.Column(column =>
			{
				wbComposer.Compose(column);
			});
		});
		if (_ssheet.Workbook.Footer != null)
		{
			page.Footer().Element(container =>
			{
				container.Column(col =>
				{
					wbComposer.ComposeFooter(col);
				});
			});
		}
		else if (_ssheet.Workbook.PageFooter != null)
		{
			page.Footer().Element(container =>
			{
				container.Column(col =>
				{
					wbComposer.ComposePageFooter(col);
				});
			});
		}
	}
}
