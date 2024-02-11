// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;
internal class JsonThicknessConverter : JsonConverter<Thickness>
{
	public override Thickness? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var str = reader.GetString();
		if (String.IsNullOrEmpty(str))
			return null;
        return Thickness.FromString(str);
	}

	public override void Write(Utf8JsonWriter writer, Thickness value, JsonSerializerOptions options)
	{
		throw new NotImplementedException();
	}
}

