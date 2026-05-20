// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.AppRuntimeBuilder;

internal static class TableExtensions
{
	public static String SqlTableName(this RuntimeTable table) => $"{table.Schema}.[{table.Name}]";
	public static String TableTypeName(this RuntimeTable table) => $"{table.Schema}.[{table.Name.Singular()}.TableType]";
	public static String DetailsTableTypeName(this RuntimeTable table, RuntimeTable parent) => $"{parent.Schema}.[{parent.Name.Singular()}.{table.Name.Singular()}.TableType]";
	public static String TypeName(this RuntimeTable table) => $"T{table.Name.Singular()}";
	public static String ItemName(this RuntimeTable table) => $"{table.Name.Singular()}";

	public static IEnumerable<RuntimeField> RealFields(this RuntimeTable table)
	{
		// ПОРЯДОК ПОЛЕЙ ВАЖЕН!!! ТИП - ОБЯЗАТЕЛЬНО!!!
		foreach (var f in table.DefaultFields())
			yield return f;
		foreach (var f in table.Fields)
			yield return new RuntimeField() { Name = f.Name, Type = f.RealType(), Length = f.RealLength(), Ref = f.Ref };
	}

	public static IEnumerable<RuntimeField> RealFieldsMap(this RuntimeTable table)
	{
		return table.RealFields().Where(f => f.Name != "Memo");
	}

	public static RuntimeField FindField(this RuntimeTable table, String name)
	{
		if (name.Contains('.'))
		{
			var ns = name.Split('.');
			name = ns[0];
		}
		return table.RealFields().First(f => f.Name == name);
	}

	public static String ParentTableName(this RuntimeTable table)
	{
		return table.DetailsParent.Name.Singular();
	}

	public static List<RuntimeField> DefaultFields(this RuntimeTable table)
	{
		return table.TableType switch
		{
			TableType.Catalog => [
				new RuntimeField() { Name = "Id", Type = FieldType.Id },
				new RuntimeField() { Name = "Void", Type = FieldType.Boolean },
				new RuntimeField() { Name = "Name", Type = FieldType.String, Length = 255 },
				new RuntimeField() { Name = "Memo", Type = FieldType.String, Length = 255 }
			],
			TableType.Document => [
				new RuntimeField() { Name = "Id", Type = FieldType.Id },
				new RuntimeField() { Name = "Done", Type = FieldType.Boolean },
				new RuntimeField() { Name = "Number", Type = FieldType.String, Length = 32 },
				new RuntimeField() { Name = "Date", Type = FieldType.Date },
				new RuntimeField() { Name = "Sum", Type = FieldType.Money },
				new RuntimeField() { Name = "Memo", Type = FieldType.String, Length = 255 }
			],
			TableType.Details => [
				new RuntimeField() { Name = "Id", Type = FieldType.Id },
				new RuntimeField() { Name = "RowNo", Type = FieldType.Int },
				new RuntimeField() { Name = table.ParentTableName(), Type = FieldType.Parent },
				new RuntimeField() { Name = "Memo", Type = FieldType.String, Length = 255 }
			],
			TableType.Journal => [
				new RuntimeField() { Name = "Id", Type = FieldType.Id },
				new RuntimeField() { Name = "Date", Type = FieldType.Date },
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

	public static IEnumerable<(String Name, RuntimeTable Table)> ReferenceTable(this RuntimeTable table, String name, RuntimeMetadata metadata)
	{
		foreach (var f in table.Fields.Where(f => f.Ref == name))
			yield return (f.Name, table);
		if (table.Details == null)
			yield break;
		foreach (var dd in table.Details)
			foreach (var f in dd.ReferenceTable(name, metadata))
				yield return f;
	}
}
