// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Reflection;

namespace A2v10.Xaml.Drawing;

[ContentProperty("Content")]
public class Diagram : UIElementBase
{
	public DrawingElementCollection Content { get; set; } = new DrawingElementCollection();
	public Size? Size { get; set; }
	public String? ViewBox { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		var div = new TagBuilder("div", "diagram");
		MergeAttributes(div, context);
		//if (Size != null)
		//div.MergeStyle("width", $"{Size.Width.ToString()}px");
		div.RenderStart(context);
		RenderDiagram(context);
		div.RenderEnd(context);
	}

	void RenderDiagram(RenderContext context)
	{
		var svg = new TagBuilder("svg");
		svg.MergeAttribute("xmlns", "http://www.w3.org/2000/svg");
		svg.MergeAttribute("shape-rendering", "geometricPrecision");
		if (Size != null)
		{
			svg.MergeAttribute("width", Size.Width.ToString());
			svg.MergeAttribute("height", Size.Height.ToString());
		}
		if (ViewBox != null)
		{
			svg.MergeAttribute("viewBox", ViewBox);
			svg.MergeAttribute("preserveAspectRatio", "xMidYMid meet");
		}

		svg.RenderStart(context);
		RenderDefs(context);
		foreach (var c in Content)
			c.RenderElement(context);
		svg.RenderEnd(context);
	}

	static void RenderDefs(RenderContext context)
	{
		var assembly = Assembly.GetExecutingAssembly();
        var resName = "A2v10.ViewEngine.Xaml.Drawing.Resources.svgdefs.html";
        using var stream = assembly.GetManifestResourceStream(resName)
            ?? throw new InvalidOperationException($"The resource '{resName}' not found. Did you remember to set 'Build Action' to 'Embedded resource'?");
        using var sr = new StreamReader(stream);
        context.Writer.Write(sr.ReadToEnd());
	}
}

