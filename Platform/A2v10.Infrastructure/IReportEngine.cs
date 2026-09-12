// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;
public interface IReportInfo
{
	Stream? Stream { get; }
	String? Name { get; }
	String Path { get; }
	String Report { get; }
	IDataModel? DataModel { get; }
	ExpandoObject? Variables { get; }
}

public interface IReportEngine
{
	Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format);
}

/* Какое расширение у бланка — ответ probe, а не имени: объявленный путь расширения не несёт
 * (называется как вид), а формат решают первые байты уже внутри файла. Список кандидатов один
 * на всех, потому что спрашивающих двое и в разных сборках: движок открывает бланк, чтобы его
 * нарисовать, слой метаданных — чтобы вычитать из того же файла секцию Model. Два списка
 * разъехались бы молча: бланк нашёлся бы для одного и не нашёлся для другого.
 */
public static class ReportTemplateFile
{
	public static readonly String[] Extensions = [".vxaml", ".xaml", ".json"];

	public static Stream Open(IAppCodeProvider provider, String path)
	{
		foreach (var ext in Extensions)
		{
			var stream = provider.FileStreamRO(path + ext);
			if (stream != null)
				return stream;
		}
		throw new InvalidOperationException(
			$"Report template not found: '{path}' ({String.Join(", ", Extensions)})");
	}
}

