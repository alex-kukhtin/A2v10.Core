
using A2v10.ReportEngine.Excel;
using A2v10.Xaml.Report.Spreadsheet;

namespace ReportEngineExcel;

internal class Program
{
	static void Main(string[] args)
	{
		String fileName = "C:\\Projects\\NovaEra.2023\\Data\\Пример_расходной_накладной.xlsx";

		var conv = new ExcelConvertor(fileName);
		var res = conv.ParseFile();

		var json = SpreadsheetJson.ToJsonDebug(res);

		Console.WriteLine(json);
	}
}
