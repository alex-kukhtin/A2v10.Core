// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace A2v10.Services.Interop;

internal partial class HtmlReader(IFormatProvider currentFormat)
{
	private IFormatProvider _currentNumberFormat = currentFormat;
	private IFormatProvider _currentDateFormat = currentFormat;

	public ExSheet ReadHtmlSheet(String html)
	{
		var doc = GetXmlFromHtml(html);
		var table = doc.FirstChild;
		if (table == null || table?.Name != "table")
			throw new ExportToExcelException("Invalid element for Html2Excel. Expected '<table>'.");

		if (table.Attributes != null) {
			foreach (var attr in table.Attributes.OfType<XmlAttribute>())
			{
				if (attr.Name == "data-num-locale")
					_currentNumberFormat = CultureInfo.CreateSpecificCulture(attr.Value);
				else if (attr.Name == "data-date-locale")
					_currentDateFormat = CultureInfo.CreateSpecificCulture(attr.Value);
			}
		}
		var sheet = new ExSheet(_currentNumberFormat, _currentDateFormat);

		var bodyRowNo = 0;
		var headerRowNo = 0;
		var footerRowNo = 0;
		foreach (var n in table.ChildNodes)
		{
			if (n is not XmlNode nd)
				continue;
			if (nd.NodeType != XmlNodeType.Element)
				continue;
			switch (nd.Name)
			{
				case "colgroup":
					foreach (var col in nd.ChildNodes.OfType<XmlNode>().Where(node => node.Name == "col"))
						AddColumn(sheet, col);
					break;
				case "tbody":
					var bodyClassAttr = nd.Attributes?["class"];
					if (bodyClassAttr?.Value == "col-shadow")
						continue; // skip shadows
					foreach (var row in nd.ChildNodes.OfType<XmlNode>().Where(node => node.Name == "tr"))
						AddRow(sheet, row, RowKind.Body, bodyRowNo++);
					break;
				case "thead":
					foreach (var row in nd.ChildNodes.OfType<XmlNode>().Where(node => node.Name == "tr"))
						AddRow(sheet, row, RowKind.Header, headerRowNo++);
					break;
				case "tfoot":
					foreach (var row in nd.ChildNodes.OfType<XmlNode>().Where(node => node.Name == "tr"))
						AddRow(sheet, row as XmlNode, RowKind.Footer, footerRowNo++);
					break;
			}
		}
		return sheet;
	}

	const String COL_PATTERN = "<col ([\\w=\"\\s:%;-]+)>";
#if NET7_0_OR_GREATER
	[GeneratedRegex(COL_PATTERN, RegexOptions.None, "en-US")]
	private static partial Regex ColumnRegex();
#else
	private static Regex COL_REGEX => new(COL_PATTERN, RegexOptions.Compiled);
	private static Regex ColumnRegex() => COL_REGEX;
#endif

	static XmlDocument GetXmlFromHtml(String html)
	{
		var xml = ColumnRegex().Replace(html, (math) => $"<col {math.Groups[1].Value} />")
			.Replace("&nbsp;", "&#160;").Replace("<br>", "&#10;").Replace("<hr>", "&#160;");
		var doc = new XmlDocument();
		doc.LoadXml(xml);
		return doc;
	}

	static String GetNodeText(XmlNode node)
	{
		if (!node.HasChildNodes)
			return node.InnerText;
		foreach (var ch in node.ChildNodes.OfType<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element))
		{
			if (ch.Attributes == null || ch.Attributes.Count == 0)
				return node.InnerText;
			var classAttr = ch.Attributes["class"];
			if (classAttr == null)
				return node.InnerText;
			if (classAttr.Value.Contains("popover-wrapper"))
				return ch.FirstChild?.InnerText ?? String.Empty;
			else if (classAttr.Value.Contains("hlink-dd-wrapper"))
				return ch.FirstChild?.FirstChild?.InnerText ?? String.Empty;
		}
		return node.InnerText;
	}

	static void AddRow(ExSheet sheet, XmlNode src, RowKind kind, Int32 rowNo)
	{
		ExRow row = sheet.GetRow(rowNo, kind);
		if (src.Attributes != null)
		{
			var classAttr = src.Attributes["class"];
			if (classAttr != null)
				row.SetRoleAndStyle(classAttr.Value);
			var heightAttr = src.Attributes["data-row-height"];
			if (heightAttr != null)
			{
				if (UInt32.TryParse(heightAttr.Value, out UInt32 height))
					row.Height = height;
			}
		}
		foreach (var cn in src.ChildNodes.OfType<XmlNode>().Where(node => node.Name == "td"))
		{
			String? dataType = null;
			String cellClass = String.Empty;
			var span = new CellSpan();

			if (cn.Attributes != null)
			{
				var colSpanAttr = cn.Attributes["colspan"];
				var rowSpanAttr = cn.Attributes["rowspan"];
				if (colSpanAttr != null)
					span.Col = Int32.Parse(colSpanAttr.Value);
				if (rowSpanAttr != null)
					span.Row = Int32.Parse(rowSpanAttr.Value);

				var dataTypeAttr = cn.Attributes["data-type"];
				if (dataTypeAttr != null)
					dataType = dataTypeAttr.Value;
				var cellClassAttr = cn.Attributes["class"];
				if (cellClassAttr != null)
					cellClass = cellClassAttr.Value;
			}

			String cellText = GetNodeText(cn);
			sheet.AddCell(rowNo, row, span, cellText, dataType, cellClass);
		}
	}

	static void AddColumn(ExSheet sheet, XmlNode src)
	{
		if (src.Attributes == null)
			return;
		var classAttr = src.Attributes["class"];
		var widthAttr = src.Attributes["data-col-width"];
		ExColumn col = sheet.AddColumn();
		if (classAttr != null)
		{
			// fit, color
		}
		if (widthAttr != null)
		{
			if (UInt32.TryParse(widthAttr.Value, out UInt32 width))
				col.Width = width;
		}
	}
}
