// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text;

using A2v10.Xaml.Report;
using A2v10.Xaml.Report.Spreadsheet;

namespace Test.PdfReportEngine;

/*
Правило целиком: формат бланка решают первые байты — '<' это XAML (страница или книга,
как скажет корень), '{' это JSON, и JSON всегда книга. Один вход на все три маршрута:
файл, текст из базы, поток.
*/

[TestClass]
[TestCategory("Template Format")]
public class TemplateFormat
{
	const String XAML_PAGE = """
		<Page xmlns="clr-namespace:A2v10.Xaml.Report;assembly=A2v10.Xaml.Report" Title="Page title"/>
		""";

	const String JSON_WORKBOOK = """
		{"Title":"Workbook title","Workbook":{"RowCount":1,"ColumnCount":2}}
		""";

	static Byte[] Utf8(String s) => Encoding.UTF8.GetBytes(s);
	static Byte[] Utf8Bom(String s) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(s)];

	[TestMethod]
	public void XamlIsAPage()
	{
		var page = TemplateReader.ReadReport(Utf8(XAML_PAGE));
		Assert.IsNotInstanceOfType<Spreadsheet>(page);
		Assert.AreEqual("Page title", page.Title);
	}

	[TestMethod]
	public void JsonIsAWorkbook()
	{
		var page = TemplateReader.ReadReport(Utf8(JSON_WORKBOOK));
		var sheet = Assert.IsInstanceOfType<Spreadsheet>(page);
		Assert.AreEqual("Workbook title", sheet.Title);
		Assert.AreEqual(2u, sheet.Workbook.ColumnCount);
	}

	// BOM ломает и XmlReader в первой позиции, и JsonSerializer в строке, а приехать он
	// может по любому маршруту: файл из VS, поле в базе
	[TestMethod]
	public void BomIsDropped()
	{
		Assert.AreEqual("Page title", TemplateReader.ReadReport(Utf8Bom(XAML_PAGE)).Title);
		Assert.AreEqual("Workbook title", TemplateReader.ReadReport(Utf8Bom(JSON_WORKBOOK)).Title);
	}

	// Значащий символ ищется за пробелами: пролог XAML тоже '<', и это по-прежнему страница
	[TestMethod]
	public void BlanksAndPrologAreSkipped()
	{
		var withProlog = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n{XAML_PAGE}";
		Assert.AreEqual("Page title", TemplateReader.ReadReport(Utf8(withProlog)).Title);
		Assert.IsInstanceOfType<Spreadsheet>(TemplateReader.ReadReport(Utf8($"\r\n\t {JSON_WORKBOOK}")));
	}

	// Секция Model — объявление выборки для слоя метаданных; рендерер её не смотрит, но
	// донести обязан: в XAML ей негде жить, кроме свойства корня
	[TestMethod]
	public void XamlCarriesTheModelSection()
	{
		var xaml = """
			<Page xmlns="clr-namespace:A2v10.Xaml.Report;assembly=A2v10.Xaml.Report">
				<Page.Model>{ "Document": [ "Number" ] }</Page.Model>
			</Page>
			""";
		var page = TemplateReader.ReadReport(Utf8(xaml));
		Assert.AreEqual("""{ "Document": [ "Number" ] }""", page.Model?.Trim());
	}

	// В JSON-бланке "Model" — объект верхнего уровня рядом с разметкой. Без [JsonIgnore] на
	// Page.Model сюда пришёл бы объект в String, и упала бы каждая нынешняя печатная форма
	[TestMethod]
	public void JsonModelSectionDoesNotReachTheStringSlot()
	{
		var json = """
			{"Model":{"Document":["Number"]},"Workbook":{"RowCount":1,"ColumnCount":1}}
			""";
		var page = TemplateReader.ReadReport(Utf8(json));
		Assert.IsInstanceOfType<Spreadsheet>(page);
		Assert.IsNull(page.Model);
	}

	[TestMethod]
	public void NeitherIsAnError()
	{
		Assert.ThrowsExactly<InvalidOperationException>(() => TemplateReader.ReadReport(Utf8("Hello")));
		Assert.ThrowsExactly<InvalidOperationException>(() => TemplateReader.ReadReport(Utf8("  \r\n")));
		Assert.ThrowsExactly<InvalidOperationException>(() => TemplateReader.ReadReport(Utf8Bom("")));
	}
}
