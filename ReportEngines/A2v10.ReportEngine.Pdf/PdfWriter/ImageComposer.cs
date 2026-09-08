// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

using Image = A2v10.Xaml.Report.Image;

internal class ImageComposer(Image _image, RenderContext _context) : FlowElementComposer
{
    internal override void Compose(IContainer container, ExpandoObject scope)
	{
		if (!_context.IsVisible(_image, scope))
			return;
		container = container.ApplyDecoration(_image.RuntimeStyle);

		Byte[]? stream;
		var rtBind = _image.GetBindRuntime("FileName");
		if (rtBind != null && rtBind.Expression != null)
		{
			var fileName = _context.Evaluate(rtBind.Expression, scope)?.ToString();
			stream = fileName != null ? _context.GetFileAsByteArray(fileName) : null;
		}
		else if (!String.IsNullOrEmpty(_image.FileName))
			stream = _context.GetFileAsByteArray(_image.FileName);
		else
			stream = _context.GetValueAsByteArray(_image, scope, "Source");
		if (stream == null)
			return;
		if (_image.Width != null)
			container = container.Width(_image.Width.Value, _image.Width.Unit.ToUnit());
		if (_image.Height != null)
			container = container.Width(_image.Height.Value, _image.Height.Unit.ToUnit());
		container.Image(stream).FitArea();
	}
}
