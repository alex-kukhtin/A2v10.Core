// Copyright © 2018 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Xaml.Drawing
{

	public class Line : DrawingElement, IHasMarkers
	{
		public LineMarkerStyle MarkerStart { get; set; }
		public LineMarkerStyle MarkerEnd { get; set; }

		internal override void RenderElement(RenderContext context)
		{
			var p = new TagBuilder("path", "line");
			MergeAttributes(p, context);
			this.SetMarkers(p);
			p.Render(context);
		}
	}
}
