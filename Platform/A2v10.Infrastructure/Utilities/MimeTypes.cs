// Copyright © 2021-2022 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public static class MimeTypes
{
	public static class Application
	{
		public const String Json = "application/json";
		public const String Javascript = "application/javascript";
		public const String OctetBinary = "application/octet-binary";
		public const String OctetStream = "application/octet-stream";
		public const String Pdf = "application/pdf";
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
		public const String Jpg = "image/jpeg";
		public const String Svg = "image/svg+xml";
		public const String Bmp = "image/bmp";
		public const String Tif = "image/tiff";
	}
	public static String GetMimeMapping(String ext)
	{
		return ext.ToLowerInvariant() switch {
			".png" => Image.Png,
			".svg" => Image.Svg,
			".bmp" => Image.Bmp,
			".tif" or ".tiff" => Image.Tif,
			".jpg" or ".jpeg" or ".jpe" => Image.Jpg,
			_  => throw new ArgumentOutOfRangeException($"Invalid Mime for {ext}")
		};
	}

	public static Boolean IsImage(String? mime)
	{
		return mime != null && mime.StartsWith("image", StringComparison.OrdinalIgnoreCase);
	}
}
