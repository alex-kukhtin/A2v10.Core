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
	Identifier,
	String,
	Money,
	Float,
	Date,
	DateTime,
	Boolean
}

public record RuntimeField
{
	public String? Name { get; init; }
	public Int32? Length { get; init; }
	public String? Ref { get; init; }
	public FieldType? Type { get; init; }

	[JsonIgnore]
	public Type ValueType =>
		Type switch
		{
			FieldType.Identifier => typeof(Int64),
			FieldType.String => typeof(String),
			FieldType.Money => typeof(Decimal),
			FieldType.Float => typeof(Double),
			FieldType.Boolean => typeof(Boolean),
			FieldType.Date or FieldType.DateTime => typeof(DateTime),
			_ => throw new NotImplementedException(Type.ToString())
		};
}
public record RuntimeTable
{
	public List<RuntimeField> Fields { get; init; } = [];

	public IEnumerable<RuntimeField> RealFields()
	{
		// ПОРЯДОК ПОЛЕЙ ВАЖЕН!!! ТИП - ОБЯЗАТЕЛЬНО!!!

		yield return new RuntimeField() { Name = "Id", Type = FieldType.Identifier };
		yield return new RuntimeField() { Name = "Name", Type = FieldType.String, Length = 255 };
		yield return new RuntimeField() { Name = "FullName", Type = FieldType.String, Length = 255 };
		yield return new RuntimeField() { Name = "Memo", Type = FieldType.String, Length = 255 };
		yield return new RuntimeField() { Name = "IsCustomer", Type = FieldType.Boolean};
		yield return new RuntimeField() { Name = "IsSupplier", Type = FieldType.Boolean };
		yield return new RuntimeField() { Name = "Folder", Type = FieldType.Identifier };
	}
}

public record RuntimeMetdata
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
}
