// Copyright © 2020-2024 Oleksandr Kukhtin. All rights reserved.


using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace A2v10.Services;

public static class JsonHelpers
{
	public static readonly JsonSerializerSettings CamelCaseSerializerSettings =
		new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore,
			ContractResolver = new DefaultContractResolver()
			{
				NamingStrategy = new CamelCaseNamingStrategy()
			}
		};

	public static readonly JsonSerializerSettings DataSerializerSettings =
		new()
		{
			Formatting = Formatting.None,
			StringEscapeHandling = StringEscapeHandling.EscapeHtml,
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};

	public static readonly JsonSerializerSettings CompactSerializerSettings =
		new()
		{
			Formatting = Formatting.None,
			StringEscapeHandling = StringEscapeHandling.Default,
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore,
			Converters =
			[
				new JsonDoubleConverter()
			]
		};

    public static readonly JsonSerializerSettings IndentedSerializerSettings =
        new()
        {
            Formatting = Formatting.Indented,
            StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
			Converters =
			[
				new JsonDoubleConverter(),
				new IgnoreNullValueExpandoObjectConverter()
			]
        };
}
