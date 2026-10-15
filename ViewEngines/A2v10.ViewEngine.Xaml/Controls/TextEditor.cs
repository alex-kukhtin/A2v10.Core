// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

// a2-text-editor (Scripts/texteditor.js): a comment, markdown of three elements. Decisions — design/texteditor.md.
// The control writes the value back, so item + prop; Delegates — an object of hooks (UploadImage, ImageUrl, Submit), as Calendar
public class TextEditor : UIElementBase
{
	public Object? Value { get; set; }
	public String? Placeholder { get; set; }
	public Boolean Disabled { get; set; }
	public Object? Delegates { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var tag = new TagBuilder("a2-text-editor", null, IsInGrid);
		onRender?.Invoke(tag);
		MergeAttributes(tag, context);
		var _ = GetBinding(nameof(Value))
			?? throw new XamlException("Value binding is required for the TextEditor element");
		MergeValueItemProp(tag, context, nameof(Value));
		MergeBindingAttributeString(tag, context, "placeholder", nameof(Placeholder), Placeholder);
		MergeBoolAttribute(tag, context, nameof(Disabled), Disabled);
		var delegatesBind = GetBinding(nameof(Delegates));
		if (delegatesBind != null)
			tag.MergeAttribute(":delegates", delegatesBind.GetPath(context));
		tag.Render(context);
	}
}
