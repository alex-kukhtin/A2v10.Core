// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

namespace A2v10.AppRuntimeBuilder;

internal static class FieldExtensions
{
	public static Type ValueType(this RuntimeField field) =>
		field.Type switch
		{
			FieldType.Id => typeof(Int64),
			FieldType.String => typeof(String),
			FieldType.Money => typeof(Decimal),
			FieldType.Float => typeof(Double),
			FieldType.Boolean => typeof(Boolean),
			FieldType.Date or FieldType.DateTime => typeof(DateTime),
			_ => throw new NotImplementedException(field.Type.ToString())
		};

	public static FieldType RealType(this RuntimeField field)
		=> field.Ref != null ? FieldType.Id :  field.Type;

	public static Int32? RealLength(this RuntimeField field)
		=> field.IsString() ? field.Length ?? 255 : null;

	public static Boolean HasMaxChars(this RuntimeField field)
		=> field.IsString() && field.RealLength() >= 255;
	
	public static Boolean IsString(this RuntimeField field)
		=> field.Ref == null && field.Type == FieldType.String;
    
	public static String MapName(this RuntimeField field)
	{
		if (field.Ref == null)
			throw new InvalidOperationException("Map Ref is null");
		return field.Ref.Split('.')[1].Singular().ToLowerInvariant();
	}
    public static String SelectSqlField(this RuntimeField field, String alias, RuntimeTable table)
	{
		if (field.Ref != null)
		{
			var refTable = table.FindTable(field.Ref);
			return $"[{field.Name}!{refTable.TypeName()}!RefId] = {alias}.[{field.Name}]";
		}
        else if (field.Name == "Id")
            return $"[Id!!Id] = {alias}.Id";
        else if (field.Name == "Name")
            return $"[Name!!Name] = {alias}.[Name]";
        else if (field.Name == "RowNo")
            return $"[RowNo!!RowNumber] = {alias}.RowNo";
        else if (field.Name == "Parent")
            return $"[!{table.DetailsParent.TypeName()}.{table.Name}!ParentId] = {alias}.Parent";
        return $"{alias}.[{field.Name}]";
	}

    public static Boolean Searchable(this FieldType fieldType) =>
		fieldType switch
		{
			FieldType.String => true,
			_ => false
		};
	public static Boolean Sortable(this FieldType fieldType) =>
		fieldType != FieldType.Boolean && fieldType != FieldType.Reference;
}
