// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using QRCoder;

using A2v10.ReportEngine.Script;
using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class QrCodeComposer(QrCode _code, RenderContext _context) : FlowElementComposer
{
	private static Byte[] CreateQrCodeStream(String strCode)
	{
		var gen = new QRCodeGenerator();
		var qrCodeData = gen.CreateQrCode(strCode, QRCodeGenerator.ECCLevel.Q);
		var pngCode = new PngByteQRCode(qrCodeData);
		return pngCode.GetGraphic(20);
	}

	public static void DrawQrCode(IContainer container, String strCode)
	{
		var stream = CreateQrCodeStream(strCode);
		container.Image(stream).FitArea();
	}

	internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_code))
			return;
		container = container.ApplyDecoration(_code.RuntimeStyle);
		if (_context.IsVisible(_code))
		{
			var strCode = _context.GetValueAsString(_code, nameof(QrCode.Value)) ?? String.Empty;

			var stream = CreateQrCodeStream(strCode);

			if (_code.Size != null)
			{
				container = container.Width(_code.Size.Value, _code.Size.Unit.ToUnit());
				container = container.Height(_code.Size.Value, _code.Size.Unit.ToUnit());
			}
			container.Image(stream).FitArea();
		}
	}
}
