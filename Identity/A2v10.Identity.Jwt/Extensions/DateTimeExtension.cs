using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Identity.Jwt
{
	public static class DateTimeExtension
	{
		public static Int64 ToUnixTime(this DateTime date)
		{
			return new DateTimeOffset(date.ToUniversalTime()).ToUnixTimeSeconds();
		}
	}
}
