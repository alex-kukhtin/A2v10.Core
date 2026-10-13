// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

// The canonical preview for _blob; FilePreview is the old _file. No Url: a blob is read by its own id and token
public class BlobPreview : UIElementBase
{
	public Object? Value { get; set; }
	public Length? Width { get; set; }
	public Length? Height { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var tag = new TagBuilder("a2-blob-preview", null, IsInGrid);
		onRender?.Invoke(tag);
		MergeAttributes(tag, context);
		var valBind = GetBinding(nameof(Value))
			?? throw new XamlException("Value binding is required for the BlobPreview element");
		tag.MergeAttribute(":value", valBind.GetPathFormat(context));
		if (Width != null)
			tag.MergeAttribute("width", Width.Value);
		if (Height != null)
			tag.MergeAttribute("height", Height.Value);
		tag.Render(context);
	}
}
