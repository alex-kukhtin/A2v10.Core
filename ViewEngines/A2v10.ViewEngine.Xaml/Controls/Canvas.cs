using System.Collections.Generic;

namespace A2v10.Xaml;

public class Canvas : UIElementBase
{
	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		var tag = new TagBuilder("canvas", null, IsInGrid);
		onRender?.Invoke(tag);
		MergeAttributes(tag, context);
		tag.Render(context, TagRenderMode.Normal);
	}
}
