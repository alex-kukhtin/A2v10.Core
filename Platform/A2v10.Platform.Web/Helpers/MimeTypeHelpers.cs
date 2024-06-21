// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

// TODO: to Infrastructure
internal static class MimeTypeHelpers
{
	public static String GetExtension(String? mimeType)
	{
		return mimeType?.ToLowerInvariant() switch
		{
			MimeTypes.Application.Pdf => ".pdf",
			MimeTypes.Application.Zip => ".zip",
			MimeTypes.Application.Xlsx => ".xlsx",
			MimeTypes.Application.Xml or MimeTypes.Text.Xml => ".xml",
			MimeTypes.Application.Json => ".json",
			MimeTypes.Text.Html or MimeTypes.Text.HtmlUtf8 => ".htm",
			MimeTypes.Text.Plain => ".txt",
			MimeTypes.Text.Csv => ".csv",
			MimeTypes.Image.Png => ".png",
			MimeTypes.Image.Jpg => ".jpg",
			MimeTypes.Image.Bmp => ".bmp",
			MimeTypes.Image.Svg => ".svg",
			MimeTypes.Image.Tif => ".tif",
			_ => String.Empty,
		};
	}
}
