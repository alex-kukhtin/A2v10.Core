// Copyright © 2018-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml.Drawing;

[ContentProperty("Content")]
public class Group : DrawingElement
{
	public DrawingElementCollection Content { get; set; } = [];

	public Boolean DropShadow { get; set; }

	internal override void RenderElement(RenderContext context)
	{
		var g = new TagBuilder("g");
		MergeAttributes(g, context);
		if (DropShadow)
		{
			g.MergeAttribute("filter", "url(#dropShadow)");
		}
		g.RenderStart(context);
		foreach (var c in Content)
			c.RenderElement(context);
		g.RenderEnd(context);
	}
}
