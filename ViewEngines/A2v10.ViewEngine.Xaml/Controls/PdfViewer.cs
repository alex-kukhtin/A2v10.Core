// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public class PdfViewer : UIElementBase
{
	public Size? Size { get; set; }
	public String? Source { get; set; }
	public Length? Height { get; init; }
	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var tag = new TagBuilder("object", "a2-pdf-viewer");

		tag.MergeAttribute("type", "application/pdf");

		if (Height != null)
			tag.MergeStyle("height", Height.Value);

		MergeAttributes(tag, context);

		if (Size != null)
		{
			if (!Size.Width.IsEmpty)
			{
				tag.MergeAttribute("width", Size.Width.ToString());
                tag.MergeStyle("width", Size.Width.ToString());
            }
            if (!Size.Height.IsEmpty)
			{
				tag.MergeAttribute("height", Size.Height.ToString());
                tag.MergeStyle("height", Size.Height.ToString());
            }
        }
		else
			tag.MergeAttribute("width", "100%");
		MergeBindingAttributeString(tag, context, "data", nameof(Source), Source);
		tag.Render(context);
	}
}
