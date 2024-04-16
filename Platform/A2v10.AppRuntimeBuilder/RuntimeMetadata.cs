// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using Newtonsoft.Json;

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
	public String Schema { get; init; } = String.Empty;
	public List<RuntimeField> Fields { get; init; } = [];

	public Dictionary<String, RuntimeTable>? Details { get; init; }
	public UserInterface? Ui { get; init; }

	private RuntimeMetadata? _metadata;
	private TableType _tableType;
	internal void SetParent(RuntimeMetadata meta, TableType tableType)
	{
		_metadata = meta;
		_tableType = tableType;
	}
	internal UserInterface GetUserInterface()
	{
		return Ui ?? new UserInterface();
	}
}

public record RuntimeMetadata
{
	public IdType Id { get; init; }
	public Dictionary<String, RuntimeTable> Catalogs { get; init; } = [];
	public Dictionary<String, RuntimeTable> Documents { get; init; } = [];

	public RuntimeTable GetTable(String tableInfo)
	{
		var info = tableInfo.Split('.');
		if (info.Length != 2)
			throw new InvalidOperationException($"Invalid runtime model {tableInfo}");
		var tableDict = info[0] switch
		{
			"Catalog" => Catalogs,
			"Documents" => Documents,
			_ => throw new InvalidOperationException($"Invalid runtime model key {info[0]}")
		};
		if (tableDict.TryGetValue(info[1], out var table))
			return table;
		throw new InvalidOperationException($"Runtime Table {info} not found");
	}

	public void OnEndInit()
	{
		foreach (var table in Catalogs.Values)
			table.SetParent(this, TableType.Catalog);
		foreach (var table in Documents.Values)
			table.SetParent(this, TableType.Document);
	}
}
