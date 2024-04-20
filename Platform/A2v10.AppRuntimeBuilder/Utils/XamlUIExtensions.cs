// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.AppRuntimeBuilder;

internal static class XamlUIExtensions
{
    const Int32 COLUMN_MAX_CHARS = 50;
    public static String BindName(this UiField field)
    {
        if (!String.IsNullOrEmpty(field.BaseField?.Ref))
            return $"{field.Name}.{field.Display ?? "Name"}";
        return field.Name;
    }
    public static DataType XamlDataType(this UiField field)
    {
		return field.BaseField?.Type switch {
			FieldType.Date => DataType.Date,
            FieldType.DateTime => DataType.DateTime,
			FieldType.Money => DataType.Currency,
			FieldType.Float => DataType.Number,
			_ => DataType.String
        };
    }

    public static Boolean IsDatePicker(this UiField field)
    {
        return field.BaseField?.Type == FieldType.Date || field.BaseField?.Type == FieldType.DateTime;
    }

    public static String RefUrl(this UiField field)
    {
        if (field.IsReference())
        {
            var sp = field.BaseField?.Ref?.Split('.')
                ?? throw new InvalidOperationException("BaseField is null");
            sp[1] = sp[1].Singular();
            return $"/{String.Join('/', sp.Select(s => s.ToLowerInvariant()))}";
        }
        throw new InvalidOperationException($"Invalid RefValue {field.Name}");
    }

    public static ColumnRole XamlColumnRole(this UiField field)
    {
        return field.BaseField?.Type switch
        {
            FieldType.Id => field.RefTable != null ? ColumnRole.Default : ColumnRole.Id,
            FieldType.Date or FieldType.DateTime => ColumnRole.Date,
            FieldType.Money or FieldType.Float => ColumnRole.Number,
            _ => ColumnRole.Default
        };
    }

    public static UIElement EditField(this UiField field, RuntimeTable table)
    {
        if (field.IsReference())
            return new SelectorSimple()
            {
                Label = field.RealTitle(),
                Url = field.RefUrl(),
                Bindings = ss =>
                {
                    ss.SetBinding(nameof(SelectorSimple.Value), new Bind($"{table.ItemName()}.{field.Name}"));
                }
            };
        else if (field.IsDatePicker())
            return new DatePicker()
            {
                Label = field.RealTitle(),
                Bindings = ss =>
                {
                    ss.SetBinding(nameof(SelectorSimple.Value), new Bind($"{table.ItemName()}.{field.Name}"));
                }
            };
        else
            return new TextBox()
            {
                Label = field.RealTitle(),
                Multiline = field.Multiline,
                Required = field.Required,
                Bindings = (txt) =>
                {
                    txt.SetBinding(
                        nameof(TextBox.Value),
                        new Bind($"{table.ItemName()}.{field.Name}") { DataType = field.XamlDataType() });
                }
            };
    }

    public static DataGridColumn IndexColumn(this UiField field)
    {
        return new DataGridColumn()
        {
            Header = field.RealTitle(),
            MaxChars = field.MaxChars ? COLUMN_MAX_CHARS : 0,
            Sort = field.Sort,
            Role = field.XamlColumnRole(),
            Bindings = c =>
            {
                c.SetBinding(nameof(DataGridColumn.Content), new Bind(field.BindName()) { DataType = field.XamlDataType() });
            }
        };
    }

    public static DataGridColumnCollection IndexColumns(this IndexUiElement index)
    {
        var coll = new DataGridColumnCollection();
        foreach (var f in index.Fields)
        {
            coll.Add(f.IndexColumn());
        }
        return coll;
    }
}
