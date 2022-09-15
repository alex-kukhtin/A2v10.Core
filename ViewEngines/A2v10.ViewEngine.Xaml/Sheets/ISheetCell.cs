// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public interface ISheetCell
{
	void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null);
	void SetParent(XamlElement parent);
	void OnSetStyles(RootContainer root);
}
