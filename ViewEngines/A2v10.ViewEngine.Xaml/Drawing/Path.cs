// Copyright © 2018-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml.Drawing;

public class Path : DrawingElement, IHasMarkers
{
	public PointCollection Points { get; set; } = [];

	public LineMarkerStyle MarkerStart { get; set; }
	public LineMarkerStyle MarkerEnd { get; set; }

	internal override void RenderElement(RenderContext context)
	{
		var p = new TagBuilder("path", "path");
		p.MergeAttribute("d", Points.ToPath());
		this.SetMarkers(p);
		p.Render(context);
	}
}
