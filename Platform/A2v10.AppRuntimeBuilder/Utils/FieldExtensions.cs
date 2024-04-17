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
	
	public static Boolean IsMultiline(this RuntimeField field)
		=> field.HasMaxChars();

	public static Boolean IsString(this RuntimeField field)
		=> field.Ref == null && field.Type == FieldType.String;

	public static String RefUrl(this RuntimeField field)
	{
		if (field.Ref != null)
		{
			var sp = field.Ref.Split('.');
			sp[1] = sp[1].Singular();
			return $"/{String.Join('/', sp.Select(s => s.ToLowerInvariant()))}";
		}
		throw new InvalidOperationException($"Invalid RefValue {field.Name}");
	}

	public static String SqlField(this RuntimeField field, String alias, RuntimeTable table)
	{
		// TODO: getParentType
		if (field.Ref != null)
			return $"[{field.Name}!TUnit!RefId] = {alias}.[{field.Name}]";
		return $"{alias}.[{field.Name}]";
	}

	public static Boolean Searchable(this FieldType fieldType) =>
		fieldType switch
		{
			FieldType.String => true,
			_ => false
		};
}
