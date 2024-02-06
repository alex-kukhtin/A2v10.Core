// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Services.Interop;

/*
Excel number formats
https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.numberingformat?view=openxml-3.0.1
*/

internal class Html2Excel(String locale)
{
	private readonly IFormatProvider _currentFormat = System.Globalization.CultureInfo.CreateSpecificCulture(locale);
	public Byte[] ConvertHtmlToExcel(String html)
	{
		var rdr = new HtmlReader(_currentFormat);
		var sheet = rdr.ReadHtmlSheet(html);
		var writer = new ExcelWriter();
		return writer.SheetToExcel(sheet);
	}

}

