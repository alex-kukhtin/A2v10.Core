// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.Jwt;

public static class DateTimeExtension
{
	public static Int64 ToUnixTime(this DateTime date)
	{
		return new DateTimeOffset(date.ToUniversalTime()).ToUnixTimeSeconds();
	}
}
