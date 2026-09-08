// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class InlinedComposer : FlowElementComposer
{
	private readonly Inlined _inlined;
	private readonly RenderContext _context;

	internal InlinedComposer(Inlined inlined, RenderContext context)
	{
		_inlined = inlined;
		_context = context;
	}

	internal override void Compose(IContainer container, ExpandoObject scope)
	{
		if (!_context.IsVisible(_inlined, scope))
			return;
		container.ApplyDecoration(_inlined.RuntimeStyle).Inlined(inl => Compose(inl, scope));
	}

	public void Compose(InlinedDescriptor inl, ExpandoObject scope)
	{
		foreach (var ch in _inlined.Children)
		{
			inl.Item().Element(elem =>
			{
				ComposeElement(elem, ch, scope);
			});
		}
	}

	void ComposeElement(IContainer container, FlowElement elem, ExpandoObject scope)
	{
		var comp = elem.CreateComposer(_context);
		comp.Compose(container, scope);
	}
}
