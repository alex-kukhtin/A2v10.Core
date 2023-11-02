// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public interface IXamlElement
{
	void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null);
}
