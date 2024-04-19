// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.AppRuntimeBuilder;

internal static class UIExtensions
{
    public static String SelectSqlField(this UiField field, String alias)
	{
		if (field.BaseField?.Ref != null)
			return $"[{field.Name}!{field.RefTable?.TypeName()}!RefId] = {alias}.[{field.Name}]";
		if (field.Name == "Id")
			return $"[Id!!Id] = {alias}.Id";
		else if (field.Name == "Name")
            return $"[Name!!Name] = {alias}.[Name]";
        return $"{alias}.[{field.Name}]";
    }

	public static String RealDisplaySqlField(this UiField field, String alias)
	{
		if (field.Display != null)
			return $"{alias}.[{field.Display}]";
		if (field.Name == "Name")
			return $"[Name!!Name] = {alias}.[Name]";
		return $"{alias}.[{field.Name}]";
	}
    public static String RealTitle(this UiField field)
	{
		if (String.IsNullOrEmpty(field.Title))
			return $"@[{field.Name}]";
		return field.Title;
	}
    public static String BindName(this UiField field)
    {
        if (!String.IsNullOrEmpty(field.BaseField?.Ref))
            return $"{field.Name}.{field.Display ?? "Name"}";
        return field.Name;
    }

    public static RuntimeTable GetTable(this EndpointDescriptor endpoint, String table)
	{
		return endpoint.Metadata?.GetTable(table) ??
			throw new InvalidOperationException($"Table {table} not found");
	}

    public static IndexUiElement GetIndexUI(this EndpointDescriptor endpoint)
	{
		var ui = endpoint.UI;
		if (ui?.Index != null)
			return ui.Index;
		var table = endpoint.BaseTable;
		var indexElem = new IndexUiElement()
		{
			Fields = [
				new UiField() {Name = "Id", Sort = true },
				new UiField() {Name = "Name", Sort = true, Search = SearchType.Like, MaxChars = true }
			]
		};
		foreach (var f in table.Fields)
		{
			indexElem.Fields.Add(new UiField() { Name = f.Name, Sort = f.Type.Sortable(),
				Search = f.Type.Searchable() ? SearchType.Like : SearchType.None,
				MaxChars = f.HasMaxChars(), BaseField = f,
				Fit = f.RealLength() <= 32 }
			);
		}
		indexElem.Fields.Add(new UiField() { Name = "Memo", Sort = true, Search = SearchType.Like, MaxChars = true,
			BaseField = table.FindField("Memo")});
		indexElem.SetParent(endpoint);
		return indexElem;
	}
}
