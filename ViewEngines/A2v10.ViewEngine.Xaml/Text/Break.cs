// Copyright © 2015-2017 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml
{
	public class Break : Inline
	{
		public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
		{
			if (SkipRender(context))
				return;
			new TagBuilder("br").Render(context, TagRenderMode.SelfClosing);
		}
	}
}
