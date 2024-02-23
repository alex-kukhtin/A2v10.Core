using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

using A2v10.Services.Interop;

namespace A2v10.Core.Web.Site;

#pragma warning disable CS9113 // Parameter is unread.
public class TestDownloadExcelHandler(IServiceProvider serviceProvider) : IClrInvokeBlob
#pragma warning restore CS9113 // Parameter is unread.
{
	// TODO: arguments
	public Task<InvokeBlobResult> InvokeAsync(ExpandoObject args)
	{
		var sh = new ExSheet();
		sh.AddColumn(100); // at least one
		sh.AddColumn(70); 
		sh.AddColumn(120);

		var row = sh.AddRow(RowKind.HeaderFlat);
		sh.AddCell(row, "T1");
		sh.AddCell(row, "T2");
		sh.AddCell(row, "T3");

		row = sh.AddRow(RowKind.BodyFlat);
		sh.AddCell(row, "Long Text without wrapping");
		sh.AddCell(row, (Decimal) 12.34);
		sh.AddCell(row, DateTime.Now);
		sh.AddCell(row, TimeSpan.FromMinutes(251));

		row = sh.AddRow(RowKind.BodyFlat);
		sh.AddCell(row, "A2");
		sh.AddCell(row, 12523.34M);
		sh.AddCell(row, DateTime.Now + TimeSpan.FromDays(5));

		row = sh.AddRow(RowKind.BodyFlat);
		sh.AddCell(row, "A2");
		sh.AddCell(row, -55M);
		sh.AddCell(row, DateTime.Today);

		var writer = new ExcelWriter();
		return Task.FromResult(new InvokeBlobResult()
		{
			Stream = writer.SheetToExcel(sh),
			Mime = MimeTypes.Application.Xlsx,
			Name = "File21.xlsx"
		});
	}
}
