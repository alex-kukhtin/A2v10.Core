// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using Azure.Identity;
using System;
using System.Collections.Generic;

namespace A2v10.AppRuntimeBuilder;

public enum IdType {
	Bigint,
	Int,
	Uniqueidentifier
}

public enum FieldType
{
	String,
	Id,
	Money,
	Float,
	Date,
	DateTime,
	Boolean
}

public enum TableType
{
	Catalog,
	Document,
	Details,
	Journal
}

public record RuntimeField
{
	public String Name { get; init; } = String.Empty;
	public Int32? Length { get; init; }
	public String? Ref { get; init; }
	public FieldType Type { get; init; }
}

public record SortElement
{
	public String Order { get; init; } = String.Empty;
}
public record IndexUiElement
{
	public SortElement? Sort {  get; init; }	
}
public record BrowseUiElement
{
}

public record UserInterface
{
	public IndexUiElement? Index { get; init; }
	public BrowseUiElement? Browse { get; init; }
}

public record RuntimeTable
{
	public String Name {  get; init; }	= String.Empty;
	public String Schema { get; private set; } = String.Empty;
	public List<RuntimeField> Fields { get; init; } = [];

	public List<RuntimeField> DefaultFields =>
		_tableType switch
		{
			TableType.Catalog => [
				new RuntimeField() { Name = "Id", Type = FieldType.Id },
				new RuntimeField() { Name = "Name", Type = FieldType.String, Length = 255 },
				new RuntimeField() { Name = "Memo", Type = FieldType.String, Length = 255 }
			],
			TableType.Document => [
				new RuntimeField() { Name = "Id", Type = FieldType.Id },
				new RuntimeField() { Name = "Done", Type = FieldType.Boolean },
				new RuntimeField() { Name = "Date", Type = FieldType.Date },
				new RuntimeField() { Name = "Memo", Type = FieldType.String, Length = 255 }
			],
			_ => throw new NotImplementedException()
		};

	public Dictionary<String, RuntimeTable>? DetailsMap { get; private set; }
	public List<RuntimeTable>? Details { get; init; }
	public UserInterface? Ui { get; init; }

	private RuntimeMetadata? _metadata;
	private TableType _tableType;
	private RuntimeTable? _parent;
	internal void SetParent(RuntimeMetadata meta, TableType tableType, RuntimeTable? parent = null)
	{
		_metadata = meta;
		_tableType = tableType;
		_parent = parent;	
		Schema = _parent != null ? _parent.Schema : tableType.TableTypeSchema();
		if (Details != null)
			foreach (var dt in Details)
			{
				DetailsMap ??= [];
				dt.SetParent(_metadata, TableType.Details, this);
				DetailsMap.Add(dt.Name, dt);
			}
	}
	internal UserInterface GetUserInterface()
	{
		return Ui ?? new UserInterface();
	}
}

public record RuntimeMetadata
{
	public IdType Id { get; init; }
	public Dictionary<String, RuntimeTable> CatalogsMap { get; init; } = [];
	public Dictionary<String, RuntimeTable> DocumentsMap { get; init; } = [];

	public List<RuntimeTable> Catalogs { get; init; } = [];
	public List<RuntimeTable> Documents { get; init; } = [];
	public RuntimeTable GetTable(String tableInfo)
	{
		var info = tableInfo.Split('.');
		if (info.Length != 2)
			throw new InvalidOperationException($"Invalid runtime model {tableInfo}");
		var tableDict = info[0] switch
		{
			"Catalog" => CatalogsMap,
			"Documents" => DocumentsMap,
			_ => throw new InvalidOperationException($"Invalid runtime model key {info[0]}")
		};
		if (tableDict.TryGetValue(info[1], out var table))
			return table;
		throw new InvalidOperationException($"Runtime Table {tableInfo} not found");
	}

	public void OnEndInit()
	{
		foreach (var table in Catalogs) {
			table.SetParent(this, TableType.Catalog);
			CatalogsMap.Add(table.Name.Singular(), table);
		}
		foreach (var table in Documents)
		{
			table.SetParent(this, TableType.Document);
			DocumentsMap.Add(table.Name.Singular(), table);
		}
	}
}
