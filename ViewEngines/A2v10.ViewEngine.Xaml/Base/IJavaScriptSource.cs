// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml;

internal interface IJavaScriptSource
{
	String? GetJsValue(RenderContext context);
}
