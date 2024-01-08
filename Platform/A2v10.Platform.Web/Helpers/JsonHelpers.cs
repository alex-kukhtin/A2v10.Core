// Copyright © 2019-2024 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

namespace A2v10.Platform.Web;

public static class JsonHelpers
{
	public static readonly JsonSerializerSettings StandardSerializerSettings =
		new()
		{
			Formatting = Formatting.Indented,
			StringEscapeHandling = StringEscapeHandling.EscapeHtml,
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};

	public static readonly JsonSerializerSettings ReleaseSerializerSettings =
		new()
		{
			Formatting = Formatting.None,
			StringEscapeHandling = StringEscapeHandling.EscapeHtml,
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};

	public static JsonSerializerSettings ConfigSerializerSettings(bool bDebug)
	{
		return bDebug ? StandardSerializerSettings : ReleaseSerializerSettings;
	}
}
