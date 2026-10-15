// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

// a2-text-view: the text of TextEditor shown. Shown, not written — :value, as BlobPreview; Delegates — ImageUrl only is asked
public class TextView : UIElementBase
{
	public Object? Value { get; set; }
	public Object? Delegates { get; set; }

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var tag = new TagBuilder("a2-text-view", null, IsInGrid);
		onRender?.Invoke(tag);
		MergeAttributes(tag, context);
		var valBind = GetBinding(nameof(Value))
			?? throw new XamlException("Value binding is required for the TextView element");
		tag.MergeAttribute(":value", valBind.GetPath(context));
		var delegatesBind = GetBinding(nameof(Delegates));
		if (delegatesBind != null)
			tag.MergeAttribute(":delegates", delegatesBind.GetPath(context));
		tag.Render(context);
	}
}
