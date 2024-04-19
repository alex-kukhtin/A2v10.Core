﻿// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.AppRuntimeBuilder;

internal static class TableExtensions
{
	public static String SqlTableName(this RuntimeTable table) => $"{table.Schema}.[{table.Name}]";
	public static String TableTypeName(this RuntimeTable table) => $"{table.Schema}.[{table.Name.Singular()}.TableType]";
	public static String TypeName(this RuntimeTable table) => $"T{table.Name.Singular()}";
	public static String ItemName(this RuntimeTable table) => $"{table.Name.Singular()}";

	public static IEnumerable<RuntimeField> SearchField(this RuntimeTable table)
		=> table.Fields.Where(f => f.Type.Searchable());

	public static IEnumerable<RuntimeField> RealFields(this RuntimeTable table)
	{
		// ПОРЯДОК ПОЛЕЙ ВАЖЕН!!! ТИП - ОБЯЗАТЕЛЬНО!!!
		foreach (var f in table.DefaultFields())
			yield return f;
		foreach (var f in table.Fields)
			yield return new RuntimeField() { Name = f.Name, Type = f.RealType(), Length = f.RealLength(), Ref = f.Ref };
	}
    public static RuntimeField FindField(this RuntimeTable table, String name)
	{
		return table.RealFields().First(f => f.Name == name);
	}

    public static List<RuntimeField> DefaultFields(this RuntimeTable table)
	{
		return table.TableType switch
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
	}

	public static String TableTypeSchema(this TableType tableType) =>
		tableType switch
		{
			TableType.Catalog => "cat",
			TableType.Document => "doc",
			TableType.Journal => "jrn",
			_ => throw new InvalidOperationException($"Invalid shema source {tableType}")
		};
}