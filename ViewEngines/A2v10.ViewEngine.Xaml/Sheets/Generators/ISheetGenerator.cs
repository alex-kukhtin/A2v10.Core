// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public interface ISheetGenerator
{
	void Generate(RenderContext context, String propertyName);
	void ApplySheetPageProps(RenderContext context, SheetPage page, String propertyName);
}
