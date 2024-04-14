// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.AppRuntimeBuilder;

internal static class TableExtensions
{
	public static String SqlTableName(this RuntimeTable table) => $"{table.Schema}.[{table.Name}]";
	public static String TableTypeName(this RuntimeTable table) => $"{table.Schema}.[{table.Name.Singular()}.TableType]";
	public static String TypeName(this RuntimeTable table) => $"T{table.Name.Singular()}";
}
