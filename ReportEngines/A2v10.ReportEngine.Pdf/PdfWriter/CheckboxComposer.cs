// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace A2v10.ReportEngine.Pdf;

internal class CheckboxComposer(Checkbox checkbox, RenderContext context) : FlowElementComposer
{
	private readonly Checkbox _checkbox = checkbox;
	private readonly RenderContext _context = context;

    internal override void Compose(IContainer container, Object? value = null)
	{
		if (!_context.IsVisible(_checkbox))
			return;

		// 1pt = 1.333(3) px
		Boolean val = GetCheckBoxValue(value);
		String checkMark = val ? "<polyline points='4,8 7,12 12,4' fill='none' stroke='black' stroke-width='2'/>" : String.Empty;
		String svgText = $"<svg viewBox='0 0 16 16'><rect x='0' y='0' width='16' height='16' fill='none' stroke-width='1.333' stroke='black'/>{checkMark}</svg>";
		var svgImage = SvgImage.FromText(svgText.Replace('\'', '"'));

		container.ApplyDecoration(_checkbox.RuntimeStyle)
			.Width(12, Unit.Point)
			.Height(12, Unit.Point)
			.Svg(svgImage);
	}

	Boolean GetCheckBoxValue(Object? scope)
	{
		var valBind = _checkbox.GetBindRuntime(nameof(_checkbox.Value));
		if (valBind != null && valBind.Expression != null)
		{
			var accessFunc = _context.Engine.CreateAccessFunction(valBind.Expression);
			if (accessFunc != null)
			{
				Object? res;
				if (scope is ExpandoObject eoScope)
					res = _context.Engine.Invoke(accessFunc, eoScope, valBind.Expression);
				else
					res = _context.Engine.EvaluateValue(valBind.Expression);
				if (res != null && res is Boolean resBool)
					return resBool;
			}
		}
		else if (_checkbox.Value != null)
		{
			return _checkbox.Value.Value;
		}
		return false;
	}
}
