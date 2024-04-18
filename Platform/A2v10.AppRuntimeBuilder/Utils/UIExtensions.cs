// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.AppRuntimeBuilder;

internal static class UIExtensions
{
	public static String RealTitle(this UiField field)
	{
		if (String.IsNullOrEmpty(field.Title))
			return $"@[{field.Name}]";
		return field.Title;
	}
	public static IndexUiElement GetIndexUI(this EndpointDescriptor endpoint)
	{
		var ui = endpoint.UI;
		if (ui.Index != null)
			return ui.Index;
		var table = endpoint.BaseTable;
		var indexElem = new IndexUiElement()
		{
			Fields = [
				new UiField() {Name = "Id", Sort = true },
				new UiField() {Name = "Name", Sort = true, Search = SearchType.Like, MaxChars = true },
			]
		};
		foreach (var f in table.Fields)
		{
			indexElem.Fields.Add(new UiField() { Name = f.Name, Sort = f.Type.Sortable(),
				Search = f.Type.Searchable() ? SearchType.Like : SearchType.None,
				MaxChars = f.HasMaxChars() }
			);
		}
		indexElem.Fields.Add(new UiField() { Name = "Memo", Sort = true, Search = SearchType.Like, MaxChars = true });
		return indexElem;
	}
}
