// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace A2v10.Xaml.Report.Spreadsheet;

public static class SpreadsheetJson
{
	static JsonSerializerOptions DefaultOpts
	{
		get
		{
			var opts = new JsonSerializerOptions()
			{
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
				Encoder = JavaScriptEncoder.Create(new UnicodeRange(0x0000, 0x7FFF))
			};
			opts.Converters.Add(new JsonStringEnumConverter());
			opts.Converters.Add(new JsonThicknessConverter());
			return opts;
		}
	}

	static JsonSerializerOptions DebugOpts
	{
		get
		{
			var opts = new JsonSerializerOptions()
			{
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
				Encoder = JavaScriptEncoder.Create(new UnicodeRange(0x0000, 0x7FFF)),
				WriteIndented = true
			};
			opts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
			opts.Converters.Add(new JsonStringEnumConverter());
			opts.Converters.Add(new JsonThicknessConverter());
			return opts;
		}
	}

	public static Spreadsheet FromJson(String json)
	{
		return JsonSerializer.Deserialize<Spreadsheet>(json, DefaultOpts)
			?? throw new InvalidOperationException("Invalid json");

	}

	public static String ToJson(Spreadsheet spreadsheet)
	{
		return JsonSerializer.Serialize(spreadsheet, DefaultOpts);
	}

	public static String ToJsonDebug(Spreadsheet spreadsheet)
	{
		return JsonSerializer.Serialize(spreadsheet, DebugOpts);
	}
}
