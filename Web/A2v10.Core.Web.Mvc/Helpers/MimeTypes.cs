// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Core.Web.Mvc
{
	public static class MimeTypes
	{
		public static class Application
		{
			public const String Json = "application/json";
			public const String Javascript = "application/javascript";
		}

		public static class Text
		{
			public const String Plain = "text/plain";
			public const String Html = "text/html";
			public const String HtmlUtf8 = "text/html; charset=UTF-8";
			public const String Css = "text/css";
		}

		public static class Image
		{
			public const String Png = "image/png";
			public const String Svg = "image/svg+xml";
		}
	}
}
