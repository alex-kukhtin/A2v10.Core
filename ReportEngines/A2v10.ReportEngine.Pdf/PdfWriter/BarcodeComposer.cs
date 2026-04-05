// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.ReportEngine.Script;
using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class BarcodeComposer(Barcode _code, RenderContext _context) : FlowElementComposer
{
	private String CreateBarcodeSvg(String strCode)
	{
        var options = new BarcodeOptions();
        if (_code.Height != 0)
            options.Height = _code.Height;
        var barcode = new EanBarcodeGenerator(options);
        return _code.Type switch
		{
			BarcodeType.Ean13 => barcode.GenerateEan13(strCode),
			BarcodeType.Ean8 => barcode.GenerateEan8(strCode),
			_ => throw new NotSupportedException($"Unsupported barcode type: {_code.Type}")
        };
	}

	public void DrawBarcode(IContainer container, String strCode)
	{
		var svg = CreateBarcodeSvg(strCode);
		container.Svg(svg).FitHeight();
	}

	internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_code))
			return;
		container = container.ApplyDecoration(_code.RuntimeStyle);
		if (_context.IsVisible(_code))
		{
			var strCode = _context.GetValueAsString(_code, nameof(Barcode.Value)) ?? String.Empty;

			var svg = CreateBarcodeSvg(strCode);

			if (_code.Width != null)
			{
				container = container.Width(_code.Width.Value, _code.Width.Unit.ToUnit());
			}
			container.Svg(svg).FitWidth();
		}
	}
}
