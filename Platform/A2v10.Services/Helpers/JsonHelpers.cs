// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.


using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace A2v10.Services
{
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
				DateTimeZoneHandling = DateTimeZoneHandling.Utc,
				NullValueHandling = NullValueHandling.Ignore,
				DefaultValueHandling = DefaultValueHandling.Ignore
			};

		public static readonly JsonSerializerSettings CompactSerializerSettings =
			new()
			{
				Formatting = Formatting.None,
				StringEscapeHandling = StringEscapeHandling.Default,
				DateFormatHandling = DateFormatHandling.IsoDateFormat,
				DateTimeZoneHandling = DateTimeZoneHandling.Utc,
				NullValueHandling = NullValueHandling.Ignore,
				DefaultValueHandling = DefaultValueHandling.Ignore,
				Converters = new List<JsonConverter>
				{
					new JsonDoubleConverter()
				}
			};
	}
}
