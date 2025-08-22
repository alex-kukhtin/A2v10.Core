﻿// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using A2v10.Data.Interfaces;

namespace A2v10.Services.Interop;

public record ExeclParseResult
{
	public ExeclParseResult(ExpandoObject data, List<String> columns)
	{
		Data = data;
		Columns = columns;
	}

	public ExpandoObject Data { get; init; }
	public List<String> Columns { get; init; }
}

public partial class ExcelParser : IDisposable
{

	public String? ErrorMessage { get; set; }
	public List<ExcelFormatError> Errors { get; } = [];

	public void Dispose()
	{
		Dispose(true);
	}

	protected virtual void Dispose(Boolean disposing)
	{

	}

	public ExeclParseResult CreateDataModel(Stream stream)
	{
		var table = new FlatTableHandler();
		return ParseFile(stream, table);
	}

	static String ReplaceUnacceptableChars(String str)
	{
		return str.Replace('.', '_').Replace('!', '_');
	}

	public ExeclParseResult ParseFile(Stream stream, ITableDescription table)
	{
		try
		{
			return ParseFileImpl(stream, table);
		}
		catch (FileFormatException ex)
		{
			String? msg = ErrorMessage;
			if (String.IsNullOrEmpty(msg))
				msg = ex.Message;
			throw new InteropException(msg);
		}
	}


	const String DATEFORMAT_PATTERN = "d{1,4}|m{1,5}|y{2,4}|h{1,2}";
#if NET7_0_OR_GREATER
	[GeneratedRegex(DATEFORMAT_PATTERN, RegexOptions.None, "en-US")]
	private static partial Regex DateFormatRegex();
#else
	private static Regex DF_REGEX => new(DATEFORMAT_PATTERN, RegexOptions.Compiled);
	private static Regex DateFormatRegex() => DF_REGEX;
#endif

	static Boolean IsDateFormat(NumberingFormat? format)
	{
		if (format == null || format.FormatCode == null)
			return false;
		var fc = format.FormatCode.Value;
		if (String.IsNullOrEmpty(fc))
			return false;
		return DateFormatRegex().Match(fc).Success;
	}
	ExeclParseResult ParseFileImpl(Stream stream, ITableDescription table)
	{
        ArgumentNullException.ThrowIfNull(table, nameof(table));

		var rv = new List<ExpandoObject>();

		List<String> columns = [];

		using (var doc = SpreadsheetDocument.Open(stream, isEditable: false))
		{
			var workBookPart = doc.WorkbookPart
				?? throw new InteropException("Invalid Excel file");
			var workBook = workBookPart.Workbook;
			var sheet = workBook.Descendants<Sheet>().First()
				?? throw new InteropException($"The workbook does not have a sheet");
			if (sheet.Id == null)
				throw new InteropException($"The workbook sheet not have an id");
			String sheetId = sheet.Id.Value
				?? throw new InteropException($"The workbook sheet not have an id");
			var workSheetPart = (WorksheetPart) workBookPart.GetPartById(sheetId);
			var sharedStringPart = workBookPart.SharedStringTablePart
				?? throw new InteropException($"The workbook does not have a SharedStringTablePart");
			var sharedStringTable = sharedStringPart.SharedStringTable;

			var hlinksDict = workSheetPart.Worksheet.Descendants<Hyperlink>()
				.ToDictionary(h => h.Reference!.ToString()!, h => h);

			var hLinkRels = workSheetPart.HyperlinkRelationships
				.ToDictionary(h => h.Id!, h => h.Uri);

            var stylesPart = workBookPart.WorkbookStylesPart;
			// This formats is NUMBER, not standard!
			var numFormats = stylesPart?.Stylesheet
				.Descendants<NumberingFormat>()?
				.GroupBy(x => x.NumberFormatId?.Value.ToString() ?? String.Empty)
				.ToDictionary(g => g.Key, g => g.First());

			var rows = workSheetPart.Worksheet.Descendants<Row>().ToList()
				?? throw new InteropException($"The sheet does not have a rows");
			var hdr = rows[0];

			var hdrCells = hdr.Elements<Cell>().ToList();
			for (var ci = 0; ci < hdrCells.Count; ci++)
			{
				var c = hdrCells[ci];
				if (c == null || c.CellValue == null)
					continue;
				if (c.DataType != null && c.DataType == CellValues.SharedString)
				{
					Int32 ssid = Int32.Parse(c.CellValue.Text);
					String str = sharedStringTable.ChildElements[ssid].InnerText;
					str = ReplaceUnacceptableChars(str);
					columns.Add(str);
				}
				else
				{
					columns.Add($"Empty-{ci}");
				}
			}
			for (var ri = 1 /*1!*/; ri < rows.Count; ri++)
			{
				var r = rows[ri];
				var dataRow = table.NewRow();
				var cells = r.Elements<Cell>().ToList();
				for (var ci = 0; ci < cells.Count; ci++)
				{
					var c = cells[ci];
					if (c == null || c.CellValue == null)
						continue;
					var colIndex = ToIndex(c.CellReference) - 1;
					if (c.DataType != null && c.DataType == CellValues.SharedString)
					{
						Int32 ssid = Int32.Parse(c.CellValue.Text);
						String str = sharedStringTable.ChildElements[ssid].InnerText;
						try
						{
							if (hlinksDict.TryGetValue(c.CellReference!.ToString()!, out Hyperlink? hLink)
								&& hLink?.Id != null && hLinkRels.TryGetValue(hLink.Id!, out Uri? hLinkUrl) && hLinkUrl != null)
							{
								table.SetValue(dataRow, columns[colIndex], hLinkUrl.ToString());
							}
							else
							{
								table.SetValue(dataRow, columns[colIndex], str);
							}
						}
						catch (Exception ex)
						{
							Errors.Add(new ExcelFormatError()
							{
								Message = ex.Message,
								CellReference = c.CellReference,
								Value = str
							});
						}
					}
					else if (c.StyleIndex is not null && c.CellValue != null)
					{
						Int32 ix = (Int32) c.StyleIndex.Value; // Int32.Parse(c.StyleIndex);
						var cellFormat = workBookPart.WorkbookStylesPart?.Stylesheet?.CellFormats?.ChildElements[ix] as CellFormat;
						var fmtId = cellFormat?.NumberFormatId?.ToString();
						if (fmtId is not null && numFormats != null && numFormats.TryGetValue(fmtId, out NumberingFormat? nf))
						{
							// number
							if (Double.TryParse(c.CellValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out Double dblVal))
							{
								if (IsDateFormat(nf))
									table.SetValue(dataRow, columns[colIndex], DateTime.FromOADate(dblVal));
								else
									table.SetValue(dataRow, columns[colIndex], dblVal);
							}
							else
								throw new InteropException($"invalid cell value '{c.CellValue.Text}' for format '{cellFormat?.InnerText}'");
						}
						else
						{
							Object cellVal = GetCellValue(c.CellValue.Text, cellFormat);
							if (cellVal != null)
								table.SetValue(dataRow, columns[colIndex], cellVal);
						}
					}
					else if (c.CellValue != null)
						table.SetValue(dataRow, columns[colIndex], c.CellValue.Text);
				}
			}
		}

		if (Errors.Count > 0)
		{
			var msg = String.Join<String>("\n", Errors.Select(x => $"{x.CellReference}: '{x.Value}' => '{x.Message}'"));
			throw new InteropException(msg);
		}

		return new ExeclParseResult(table.ToObject(), columns);
	}

	static Object GetCellValue(String text, CellFormat? format)
	{
		if (format == null)
			return text;
		var formatId = format.NumberFormatId;
		var strFormat = ExcelFormats.GetDateTimeFormat(formatId);
		if (!String.IsNullOrEmpty(strFormat))
		{
			if (Double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out Double dblDate))
				return DateTime.FromOADate(dblDate);
		}
		else if (ExcelFormats.IsNumberFormat(formatId))
		{
			// hack
			if (Double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out Double dblVal))
				return dblVal;
		}
		return text;
	}

	static Int32 ToIndex(String? refs)
	{
		if (refs == null) 
			return 0;
		Int32 ci = 0;
		refs = refs.ToUpper();
		for (Int32 ix = 0; ix < refs.Length && refs[ix] >= 'A'; ix++)
			ci = (ci * 26) + ((Int32)refs[ix] - 64);
		return ci;
	}
}
