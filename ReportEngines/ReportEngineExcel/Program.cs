using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using A2v10.ReportEngine.Pdf;

namespace ReportEngineExcel;

internal class Program
{
	static void Main(string[] args)
	{
		String fileName = "C:\\Projects\\NovaEra.2023\\Data\\Пример_расходной_накладной.xlsx";

		var conv = new ExcelConvertor(fileName);
		var res = conv.ParseFile();

		var opts = new JsonSerializerOptions()
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
			Encoder = JavaScriptEncoder.Create(new UnicodeRange(0x0000, 0x7FFF))
		};
		opts.Converters.Add(new JsonStringEnumConverter());
		opts.Converters.Add(new JsonThicknessConverter());

		var json = JsonSerializer.Serialize(res, opts);

		Console.WriteLine(json);
	}
}
