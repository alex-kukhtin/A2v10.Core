﻿// Copyright © 2015-2017 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml
{
	public class StaticImage : Inline
	{
		public String? Url { get; set; }
		public Length? Height { get; set; }

		public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
		{
			if (SkipRender(context))
				return;
			var img = new TagBuilder("a2-static-image", null, IsInGrid);
			onRender?.Invoke(img);
			MergeAttributes(img, context);
			if (Height != null)
				img.MergeStyle("height", Height.Value);
			var urlBind = GetBinding(nameof(Url));
			if (urlBind != null)
				img.MergeAttribute(":url", urlBind.GetPathFormat(context));
			else if (!String.IsNullOrEmpty(Url))
				img.MergeAttribute("url", Url);
			else
				throw new XamlException("Url is required for the StaticImage element");
			img.Render(context);
		}
	}
}
