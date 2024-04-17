// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

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
		foreach (var f in table.DefaultFields)
			yield return f;
		foreach (var f in table.Fields)
			yield return new RuntimeField() { Name = f.Name, Type = f.RealType(), Length = f.RealLength() };
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
