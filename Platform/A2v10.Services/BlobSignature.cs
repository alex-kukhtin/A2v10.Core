// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services;

/* The types a blob may be served inline as - raster and PDF - recognized by the first bytes. Only
 * these: an inline Mime comes from the bytes, never from a name. Office files are not here - they
 * are always an attachment, and docx/xlsx share one ZIP signature
 */
internal static class BlobSignature
{
	public const Int32 HeaderLength = 12;

	static readonly String[] _known = [MimeTypes.Image.Png, MimeTypes.Image.Jpg, MimeTypes.Image.Gif,
		MimeTypes.Image.Webp, MimeTypes.Application.Pdf];

	public static String? Detect(ReadOnlySpan<Byte> h)
	{
		if (h.StartsWith((ReadOnlySpan<Byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
			return MimeTypes.Image.Png;
		if (h.StartsWith((ReadOnlySpan<Byte>)[0xFF, 0xD8, 0xFF]))
			return MimeTypes.Image.Jpg;
		if (h.StartsWith("GIF8"u8))
			return MimeTypes.Image.Gif;
		if (h.Length >= 12 && h.StartsWith("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
			return MimeTypes.Image.Webp;
		if (h.StartsWith("%PDF-"u8))
			return MimeTypes.Application.Pdf;
		return null;
	}

	// a type Detect would have recognized: claimed by the name, not confirmed by the bytes - refused
	public static Boolean IsKnown(String mime) =>
		_known.Contains(mime, StringComparer.OrdinalIgnoreCase);
}
