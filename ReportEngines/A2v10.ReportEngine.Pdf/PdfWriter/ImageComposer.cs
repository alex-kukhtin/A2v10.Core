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

		// Source и FileName — одно место с двумя написаниями: дальше резолвера различие
		// не идёт, он сам решает, байты это или имя файла
		var image = _context.ResolveImage(_image, scope, nameof(Image.Source), _image.Source)
			?? _context.ResolveImage(_image, scope, nameof(Image.FileName), _image.FileName);
		if (image == null)
			return;
		if (_image.Width != null)
			container = container.Width(_image.Width.Value, _image.Width.Unit.ToUnit());
		if (_image.Height != null)
			container = container.Height(_image.Height.Value, _image.Height.Unit.ToUnit());
		container.DrawImage(image);
	}
}
