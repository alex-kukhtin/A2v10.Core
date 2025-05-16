// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public class Separator : UIElementBase
{
	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		new TagBuilder("div", "divider")
			.MergeAttribute("role", "separator")
			.Render(context);
	}
}
