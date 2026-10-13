// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public class BlobImage : UIElementBase
{
	public String? Url { get; set; }
	public Object? Value { get; set; }
	public Length? Width { get; set; }
	public Length? Height { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var tag = new TagBuilder("a2-blob-image", null, IsInGrid);
		onRender?.Invoke(tag);
		MergeAttributes(tag, context);

		// The control writes the value back, so item + prop (as Image.Source), not :value (as FileImage);
		// prop is also the door key
		var _ = GetBinding(nameof(Value))
			?? throw new XamlException("Value binding is required for the BlobImage element");
		MergeValueItemProp(tag, context, nameof(Value));
		MergeBindingAttributeString(tag, context, "url", nameof(Url), Url);
		if (Width != null)
			tag.MergeAttribute("width", Width.Value);
		if (Height != null)
			tag.MergeAttribute("height", Height.Value);
		tag.Render(context);
	}
}
