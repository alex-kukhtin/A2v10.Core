// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using System;

namespace A2v10.ReportEngine.Pdf;

internal class ImageComposer : FlowElementComposer
{
	private readonly Image _image;
	private readonly RenderContext _context;

	public ImageComposer(Image image, RenderContext context)
	{
		_image = image;
		_context = context;
	}

	void ApplyRuntimeStyle(TextSpanDescriptor descr, ContentElement elem)
	{
	}

	internal override void Compose(IContainer container)
	{
		if (!_context.IsVisible(_image))
			return;
		container = container.ApplyDecoration(_image.RuntimeStyle);
		if (_context.IsVisible(_image))
		{
			Byte[]? stream;
			var rtBind = _image.GetBindRuntime("FileName");
			if (rtBind != null)
			{
				var fileName = _context.Engine.EvaluateValue(rtBind.Expression)?.ToString();
				stream = _context.GetFileAsByteArray(fileName);
			}
			else if (!String.IsNullOrEmpty(_image.FileName))
				stream = _context.GetFileAsByteArray(_image.FileName);
			else
				stream = _context.GetValueAsByteArray(_image, "Source");
			if (stream == null)
				return;
			if (_image.Width != null)
				container = container.Width(_image.Width.Value, _image.Width.Unit.ToUnit());
			if (_image.Height != null)
				container = container.Width(_image.Height.Value, _image.Height.Unit.ToUnit());
			container.Image(stream, ImageScaling.FitArea);
		}
	}
}
