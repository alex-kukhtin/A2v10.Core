// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.AppRuntimeBuilder;

internal static class TableExtensions
{
	public static String SqlTableName(this RuntimeTable table) => $"{table.Schema}.[{table.Name}]";
	public static String TableTypeName(this RuntimeTable table) => $"{table.Schema}.[{table.Name.Singular()}.TableType]";
	public static String TypeName(this RuntimeTable table) => $"T{table.Name.Singular()}";
	public static String ItemName(this RuntimeTable table) => $"{table.Name.Singular()}";

	public static IEnumerable<RuntimeField> RealFields(this RuntimeTable table)
	{
		// ПОРЯДОК ПОЛЕЙ ВАЖЕН!!! ТИП - ОБЯЗАТЕЛЬНО!!!
		yield return new RuntimeField() { Name = "Id", Type = FieldType.Id };
		yield return new RuntimeField() { Name = "Name", Type = FieldType.String, Length = 255 };
		yield return new RuntimeField() { Name = "Memo", Type = FieldType.String, Length = 255 };
		foreach (var f in table.Fields)
			yield return new RuntimeField() { Name = f.Name, Type = f.RealType(), Length = f.RealLength() };
	}
}
