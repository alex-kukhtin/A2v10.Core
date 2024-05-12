// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

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
		if (!String.IsNullOrEmpty(field.Title))
			return field.Title;
		if (field.Name.Contains('.'))
		{
			return $"@[{field.Name.Split('.')[1]}]";
		}
		return $"@[{field.Name}]";
	}

	public static Boolean IsReference(this UiField field)
	{
		return field.BaseField?.Ref != null;
	}
	public static Boolean IsSubField(this UiField field)
	{
		return field.Name.Contains('.');
	}
	public static Boolean IsPeriod(this UiField field)
	{
		return field.Name == "Date";
	}

	public static RuntimeTable GetTable(this EndpointDescriptor endpoint, String table)
	{
		return endpoint.Metadata?.GetTable(table) ??
			throw new InvalidOperationException($"Table {table} not found");
	}

    public static TableType EndpointType(this EndpointDescriptor endpoint)
    {
        if (endpoint.Table.StartsWith("Catalog.", StringComparison.OrdinalIgnoreCase))
            return TableType.Catalog;
        else if (endpoint.Table.StartsWith("Document.", StringComparison.OrdinalIgnoreCase))
            return TableType.Document;
        throw new InvalidOperationException($"Invalid Edpoint type {endpoint.Name}");
    }


	public static Boolean IsParameter(this EndpointDescriptor endpoint, RuntimeField field)
	{
		return endpoint.Parameters != null && endpoint.Parameters.ContainsKey(field.Name);
	}

    public static IndexUiElement GetIndexUI(this EndpointDescriptor endpoint)
	{
		var ui = endpoint.UI;
		if (ui.Index != null)
			return ui.Index;
		ui.Index = endpoint.DefaultIndexUiElement();
		return ui.Index;
	}
    public static IndexUiElement GetBrowseUI(this EndpointDescriptor endpoint)
    {
        var ui = endpoint.UI;
        if (ui.Browse != null)
            return ui.Browse;
        ui.Browse = endpoint.DefaultIndexUiElement();
		return ui.Browse;
    }

    public static EndpointEdit EditMode(this EndpointDescriptor endpoint)
	{
		if (endpoint.Edit == EndpointEdit.Auto)
			return endpoint.EndpointType() == TableType.Document ? EndpointEdit.Page : EndpointEdit.Dialog;
		return endpoint.Edit;
    }


	public static EditUiElement GetEditUI(this EndpointDescriptor endpoint)
	{
		var editElem = endpoint.UI.Edit;
		if (editElem == null)
			editElem = endpoint.DefaultEditUiElement();
		else if (editElem.Fields == null || editElem.Fields.Count == 0)
			editElem = endpoint.DefaultEditUiElement(editElem);
		else
			endpoint.CheckDefaultDetails(editElem);
		editElem.SetParent(endpoint);
		endpoint.UI.Edit = editElem;
		return editElem;
	}
}
