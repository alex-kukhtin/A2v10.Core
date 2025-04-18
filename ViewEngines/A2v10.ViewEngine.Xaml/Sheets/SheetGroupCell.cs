﻿// Copyright © 2015-2025 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public class SheetGroupCell : SheetCell
{
	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var td = new TagBuilder("td", "group-cell");
		td.MergeAttribute(":class", "row.cssClass()");
		td.RenderStart(context);
		var inner = new TagBuilder("div");
		inner.MergeAttribute("v-if", "row.hasChildren()");
		inner.MergeAttribute("@click.prevent", "row.toggle()");
		inner.RenderStart(context);
		new TagBuilder("i", "ico").Render(context);
		inner.RenderEnd(context);
		td.RenderEnd(context);
	}
}
