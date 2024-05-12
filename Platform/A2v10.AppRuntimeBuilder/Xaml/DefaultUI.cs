// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.AppRuntimeBuilder;

internal static class DefaultUI
{
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
					new UiField() {Name = "Id", Sort = true, Search = SearchType.Exact },
					new UiField() {Name = "Name", Sort = true, Search = SearchType.Like, LineClamp = 2 }
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
				LineClamp = f.HasLineClamp() ? 2 : 0,
				BaseField = f,
				Fit = f.RealLength() <= Constants.FitThreshold,
				Filter = f.Ref != null
			});
		}
		indexElem.Fields.Add(new UiField() { Name = "Memo", Sort = true, Search = SearchType.Like, LineClamp = 2 });
		indexElem.SetParent(endpoint);
		return indexElem;
	}

	public static List<UiField> DetailsFields(RuntimeTable rt)
	{
		bool hasQtyPriceSum = rt.Fields.Any(f => f.Name == "Qty")
			&& rt.Fields.Any(f => f.Name == "Price")
			&& rt.Fields.Any(f => f.Name == "Sum");

		List<UiField> list = [
			new() {Name = "RowNo"},
			];
		foreach (var f in rt.Fields)
			list.Add(new()
			{
				Name = f.Name,
				Computed = hasQtyPriceSum && f.Name == "Sum" ? "this.Price * this.Qty" : null,
				Total = f.Name == "Sum"
			});
		list.Add(new()
		{
			Name = "Memo",
			Multiline = true
		});
		return list;
	}

	public static EditUiElement DefaultEditUiElement(this EndpointDescriptor endpoint, EditUiElement? source = null)
	{
		var table = endpoint.BaseTable;
		List<UiField> CatalogFields()
		{
			var list = new List<UiField>()
			{
				new() {Name = "Name", Required = true}
			};
			foreach (var f in table.Fields)
			{
				list.Add(new UiField()
				{
					Name = f.Name
				});
			}
			list.Add(new UiField()
			{
				Name = "Memo",
				Multiline = true
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

		List<EditUiElement>? DetailsUIList()
		{
			if (table.Details == null || table.Details.Count == 0)
				return null;
			var list = new List<EditUiElement>();
			foreach (var dt in table.Details)
			{
				EditUiElement? existintElem = null;
				if (source != null)
					existintElem = source.Details?.FirstOrDefault(f => f.Name == dt.Name);
				if (existintElem != null)
				{
					existintElem.SetParentTable(dt, endpoint);
					list.Add(existintElem);
				}
				else
				{ 
					var uiElem = new EditUiElement()
					{
						Name = dt.Name,
						Fields = DetailsFields(dt)
					};
					uiElem.SetParentTable(dt, endpoint);
					list.Add(uiElem);
				}
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

	public static void CheckDefaultDetails(this EndpointDescriptor endpoint, EditUiElement editElem)
	{
		if (editElem.Details == null || editElem.Details.Count == 0)
			return;
		List<EditUiElement> newDetails = [];
		foreach (var dd  in editElem.Details)
		{
			if (dd.Fields.Count != 0)
				newDetails.Add(dd);
			else
			{
				if (dd.BaseTable == null)
					throw new InvalidOperationException("Details. BaseTable is null");
				var uiElem = new EditUiElement()
				{
					Name = dd.Name,
					Fields = DetailsFields(dd.BaseTable)
				};
				uiElem.SetParentTable(dd.BaseTable, endpoint);
				newDetails.Add(uiElem);
			}
		}
		editElem.Details.Clear();
		foreach (var nd in newDetails)
			editElem.Details.Add(nd);
	}
}
