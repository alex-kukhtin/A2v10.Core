// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.AppRuntimeBuilder;

internal static class XamlUIExtensions
{
    public const Int32 COLUMN_MAX_CHARS = 50;
    public static String BindName(this UiField field)
    {
        if (!String.IsNullOrEmpty(field.BaseField?.Ref))
        {
            if (field.Name.Contains('.'))
                return field.Name;
            return $"{field.Name}.{field.Display ?? "Name"}";
        }
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
    public static Length? XamlColumnWidth(this UiField field)
    {
        if (field.Name == "RowNo")
            return Length.FromString("25px");
        var fw = field.FieldWidth();
        if (fw != null)
            return fw;
        return field.BaseField?.Type switch
        {
            FieldType.Date => Length.FromString("10rem"),
            FieldType.Money or FieldType.Float => Length.FromString("6rem"),
            _ => null
        };
    }

    public static Boolean IsDatePicker(this UiField field)
    {
        return field.BaseField?.Type == FieldType.Date || field.BaseField?.Type == FieldType.DateTime;
    }
    public static TextAlign TextAlign(this UiField field)
    {
        if (field.Align != Xaml.TextAlign.Default)
            return field.Align;
        if (field.IsReference())
            return Xaml.TextAlign.Default;
        return field.BaseField?.Type switch
        {
            FieldType.Date or FieldType.DateTime or FieldType.Boolean => Xaml.TextAlign.Center,
            FieldType.Float or FieldType.Money or FieldType.Id or FieldType.Int => Xaml.TextAlign.Right,
            _ => Xaml.TextAlign.Default,
        };
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

    public static UIElement EditField(this UiField field, String? itemName = null)
    {
        var elemName = itemName != null ? $"{itemName}." : String.Empty;
        if (field.IsReference())
        {
            if (field.IsSubField())
            {
                var ff = field.Name.Split('.');
                return new SelectorSimple()
                {
                    Label = field.RealTitle(),
                    DisplayProperty = ff[1],
                    Url = field.RefUrl(),
                    Bindings = tb =>
                    {
                        tb.SetBinding(nameof(SelectorSimple.Value), new Bind($"{elemName}{ff[0]}"));
                    }
                };
            }
            else
                return new SelectorSimple()
                {
                    Label = field.RealTitle(),
                    Url = field.RefUrl(),
                    Bindings = ss =>
                    {
                        ss.SetBinding(nameof(SelectorSimple.Value), new Bind($"{elemName}{field.Name}"));
                    }
                };
        }
        else if (field.IsDatePicker())
            return new DatePicker()
            {
                Label = field.RealTitle(),
                Bindings = ss =>
                {
                    ss.SetBinding(nameof(DatePicker.Value), new Bind($"{elemName}{field.Name}"));
                }
            };
        else
            return new TextBox()
            {
                Label = field.RealTitle(),
                Multiline = field.Multiline,
                Rows = field.Multiline ? 3 : 1,
                Align = field.TextAlign(),
                Required = field.Required,
                Disabled = !String.IsNullOrEmpty(field.Computed),
                TabIndex = field.Name == "Name" ? 1 : 0,
                Bindings = (txt) =>
                {
                    txt.SetBinding(
                        nameof(TextBox.Value),
                        new Bind($"{elemName}{field.Name}") { DataType = field.XamlDataType() });
                }
            };
    }

    public static Length? FieldWidth(this UiField field)
    {
        return String.IsNullOrEmpty(field.Width) ? null : Length.FromString(field.Width);    
    }
    public static UIElement EditCellField(this UiField field, String? itemName = null)
    {
        var elemName = itemName != null ? $"{itemName}." : String.Empty;
        if (field.IsReference())
        {
            if (field.IsSubField())
                return new TextBox()
                {
                    Width = field.FieldWidth(),
					Bindings = ss =>
					{
						ss.SetBinding(nameof(TextBox.Value), new Bind($"{elemName}{field.Name}"));
					}
				};
            else
                return new SelectorSimple()
                {
                    Url = field.RefUrl(),
                    DisplayProperty = field.Display,
                    Width = field.FieldWidth(),
                    Bindings = ss =>
                    {
                        ss.SetBinding(nameof(SelectorSimple.Value), new Bind($"{elemName}{field.Name}"));
                    }
                };
        }
        else if (field.IsDatePicker())
            return new DatePicker()
            {
                Bindings = ss =>
                {
                    ss.SetBinding(nameof(SelectorSimple.Value), new Bind($"{elemName}{field.Name}"));
                }
            };
        else
            return new TextBox()
            {
                Align = field.TextAlign(),
                Disabled = !String.IsNullOrEmpty(field.Computed),
                Bindings = (txt) =>
                {
                    txt.SetBinding(
                        nameof(TextBox.Value),
                        new Bind($"{elemName}{field.Name}") { DataType = field.XamlDataType() });
                }
            };
    }

    public static DataGridColumn IndexColumn(this UiField field)
    {
        return new DataGridColumn()
        {
            Header = field.RealTitle(),
            LineClamp = field.LineClamp,
            Sort = field.Sort,
            Align = field.TextAlign(),
            Role = field.XamlColumnRole(),
            Fit = field.Fit,
            Wrap = field.Fit ? WrapMode.NoWrap : WrapMode.Default,
            Bindings = c =>
                c.SetBinding(nameof(DataGridColumn.Content), new Bind(field.BindName()) { DataType = field.XamlDataType() })
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
