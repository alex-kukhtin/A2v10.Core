// Copyright © 2022-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Text;

using A2v10.System.Xaml;
using A2v10.Xaml.Report.Spreadsheet;

namespace A2v10.Xaml.Report;

public class TemplateReader
{
	// Формат бланка решают первые байты, а не расширение и не маршрут: у текста из базы имени
	// нет вовсе, а маршрутов три (файл, база, поток) — два механизма на один вопрос со временем
	// разъезжаются. '<' — XAML, и там корневой элемент сам скажет, страница это или книга;
	// '{' — JSON, и это всегда книга: у Page разметка помечена [JsonIgnore], страницей JSON быть
	// не может. Расширение осталось при своей работе — назвать кандидатов для probe
	public static Page ReadReport(Stream stream)
	{
		using var mem = new MemoryStream();
		stream.CopyTo(mem);
		return ReadReport(mem.ToArray());
	}

	public static Page ReadReport(Byte[] bytes)
	{
		// BOM уходит вместе с началом: XmlReader падает на нём в первой позиции, а
		// JsonSerializer — на U+FEFF в строке; знает о нём только распознаватель
		var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
		var sig = start;
		while (sig < bytes.Length && bytes[sig] <= 0x20)
			sig++;
		if (sig >= bytes.Length)
			throw new InvalidOperationException("Report template is empty");
		var page = bytes[sig] switch
		{
			(Byte)'<' => FromXaml(new MemoryStream(bytes, start, bytes.Length - start)),
			(Byte)'{' => FromJson(Encoding.UTF8.GetString(bytes, start, bytes.Length - start)),
			_ => throw new InvalidOperationException(
				$"Report template is neither XAML nor JSON: it starts with '{(Char)bytes[sig]}'")
		};
		// Стили применяются здесь, а не у вызывающего: иначе книга из XAML и книга из JSON
		// получают их в разные моменты, и это ровно то, из-за чего маршруты разъезжались
		page.ApplyStyles("Root", new StyleBag());
		return page;
	}

	private static Page FromXaml(Stream stream)
	{
		var xamlReader = new XamlReaderService();
		var obj = xamlReader.Load(stream);
		if (obj is Page objPage)
			return objPage;
		throw new InvalidOperationException("Object is not a A2v10.Xaml.Report.Page");
	}

	private static Page FromJson(String json)
	{
		return SpreadsheetJson.FromJson(json);
	}
}
