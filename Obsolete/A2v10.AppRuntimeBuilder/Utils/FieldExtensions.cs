// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.AppRuntimeBuilder;

internal static class FieldExtensions
{
	public static Type ValueType(this RuntimeField field) =>
		field.Type switch
		{
			FieldType.Id or FieldType.Parent => typeof(Int64),
            FieldType.Int => typeof(Int32),
            FieldType.String => typeof(String),
			FieldType.Money => typeof(Decimal),
			FieldType.Float => typeof(Double),
			FieldType.Boolean => typeof(Boolean),
			FieldType.Date or FieldType.DateTime => typeof(DateTime),
			_ => throw new NotImplementedException($"Unknown field type: {field.Type}")
		};

	public static Boolean CanStorno(this RuntimeField field) =>
		field.Type switch {
			FieldType.Float or FieldType.Money => true,
			_ => false
		};
	public static FieldType RealType(this RuntimeField field)
		=> field.Ref != null ? FieldType.Id :  field.Type;

	public static Int32? RealLength(this RuntimeField field)
		=> field.IsString() ? field.Length ?? 255 : null;

	public static Boolean HasLineClamp(this RuntimeField field)
		=> field.IsString() && field.RealLength() >= Constants.ClampThreshold;
	
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
        else if (field.Type == FieldType.Parent)
            return $"[!{table.DetailsParent.TypeName()}.{table.Name}!ParentId] = {alias}.[{field.Name}]";
        else if (field.Name == "Id")
            return $"[Id!!Id] = {alias}.Id";
        else if (field.Name == "Name")
            return $"[Name!!Name] = {alias}.[Name]";
        else if (field.Name == "RowNo")
            return $"[RowNo!!RowNumber] = {alias}.RowNo";
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
