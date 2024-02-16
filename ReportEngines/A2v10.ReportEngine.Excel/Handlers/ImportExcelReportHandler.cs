
using System;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Xaml.Report.Spreadsheet;

namespace A2v10.ReportEngine.Excel;

#pragma warning disable CS9113 // Parameter is unread.
public class ImportExcelReportHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
#pragma warning restore CS9113 // Parameter is unread.
{
	public Task<Object> InvokeAsync(ExpandoObject args)
	{
		var blobObj = args.Get<Object>("Blob");
		if (blobObj is not IBlobUpdateInfo blobUpdateInfo)
			throw new InvalidOperationException("Invalid blob args");
		if (blobUpdateInfo.Stream == null)
			throw new InvalidOperationException("Steam is null");

		var cnv = new ExcelConvertor(blobUpdateInfo.Stream);
		var result = cnv.ParseFile();
		var json = SpreadsheetJson.ToJson(result);	

		return Task.FromResult<Object>(json);
	}
}
