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
		if (String.IsNullOrEmpty(field.Title))
			return $"@[{field.Name}]";
		return field.Title;
	}

	public static Boolean IsReference(this UiField field)
	{
		return field.BaseField?.Ref != null;
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


    public static IndexUiElement DefaultIndexUiElement(this EndpointDescriptor endpoint)
	{
        var table = endpoint.BaseTable;
		var endpointType = endpoint.EndpointType();
        IndexUiElement indexElem = endpointType switch
		{
			TableType.Catalog => new IndexUiElement()
			{
				Sort = new SortElement() { Order = "Name", Dir = SortDir.Asc },
				Fields = [
					new UiField() {Name = "Id", Sort = true },
					new UiField() {Name = "Name", Sort = true, Search = SearchType.Like, MaxChars = true }
				]
			},
			TableType.Document => new IndexUiElement()
			{
				Sort = new SortElement() { Order = "Date", Dir = SortDir.Desc },
				Fields = [
					new UiField() {Name = "Id", Sort = true },
					new UiField() {Name = "Number", Sort = true, Search = SearchType.Like, Fit = true },
					new UiField() {Name = "Date", Sort = true, Filter = true },
					new UiField() {Name = "Sum", Sort = true }
				]
			},
			_ => throw new InvalidOperationException($"Invalid Endpoint for index {endpoint.EndpointType()}")
		};

		foreach (var f in table.Fields.Where(f => !endpoint.IsParameter(f)))
        {
			indexElem.Fields.Add(new UiField()
			{
				Name = f.Name,
				Sort = f.Type.Sortable(),
				Search = f.Type.Searchable() ? SearchType.Like : SearchType.None,
				MaxChars = f.HasMaxChars(),
				BaseField = f,
				Fit = f.RealLength() <= 32,
				Filter = f.Ref != null
			});
        }
        indexElem.Fields.Add(new UiField() { Name = "Memo", Sort = true, Search = SearchType.Like, MaxChars = true });
        indexElem.SetParent(endpoint);
		return indexElem;
    }
	public static Boolean IsParameter(this EndpointDescriptor endpoint, RuntimeField field)
	{
		return endpoint.Parameters != null && endpoint.Parameters.ContainsKey(field.Name);
	}


	public static EditUiElement DefaultEditUiElement(this EndpointDescriptor endpoint)
    {
		var table = endpoint.BaseTable;
		List<UiField> CatalogFields()
		{
			var list = new List<UiField>()
			{
				new() {Name = "Name", Required = true}
			};
			foreach (var f in table.Fields) {
				list.Add(new UiField()
					{ 
						Name = f.Name 
					});
			}
			list.Add(new UiField()
			{
				Name = "Memo", Multiline = true
			});
			return list;
        }
        List<UiField> DocumentFields()
        {
			String? computedSum = null;
			if (table.Details?.Count == 1 && table.Details[0].Fields.Any(f => f.Name == "Sum"))
				computedSum = $"this.{table.Details[0].Name}.$sum(r => r.Sum)";

            List<UiField> list = [
                new() {Name = "Date"},
				new() {Name = "Number" },
                new() {Name = "Sum", Computed = computedSum }
            ];
            foreach (var f in table.Fields.Where(f => !endpoint.IsParameter(f)))
                list.Add(new() { Name = f.Name });
            list.Add(new()
            {
                Name = "Memo",
                Multiline = true
            });
            return list;
        }

        List<UiField> DetailsFields(RuntimeTable rt)
        {
			bool hasQtyPriceSum = rt.Fields.Any(f => f.Name == "Qty")
				&& rt.Fields.Any(f => f.Name == "Price")
				&& rt.Fields.Any(f => f.Name == "Sum");

            List<UiField> list = [
                new() {Name = "RowNo"},
            ];
            foreach (var f in rt.Fields)
                list.Add(new() { 
					Name = f.Name, 
					Computed = hasQtyPriceSum && f.Name == "Sum" ? "this.Price * this.Qty": null,
					Total = f.Name == "Sum"
				});
            list.Add(new()
            {
                Name = "Memo",
                Multiline = true
            });
            return list;
        }

        List<EditUiElement>? DetailsUIList()
		{
			if (table.Details == null || table.Details.Count == 0)
				return null;
			var list = new List<EditUiElement>();
			foreach (var dt in table.Details)
			{
				var uiElem = new EditUiElement()
				{
					Name = dt.Name,
					Fields = DetailsFields(dt)
				};
				uiElem.SetParentTable(dt, endpoint);
				list.Add(uiElem);
			}
			return list;
		}

		return endpoint.EndpointType() switch
		{
			TableType.Catalog => new EditUiElement()
			{
				Fields = CatalogFields(),
				Details = DetailsUIList()
			},
			TableType.Document => new EditUiElement()
			{
				Fields = DocumentFields(),
                Details = DetailsUIList()
            },
			_ => throw new InvalidOperationException($"Invalid Endpoint for index {endpoint.EndpointType()}")
		}; ;
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
		if (editElem != null)
			return editElem;
        editElem = endpoint.DefaultEditUiElement();
        editElem.SetParent(endpoint);
        endpoint.UI.Edit = editElem;
        return editElem;
    }
}
