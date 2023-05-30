// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.Xaml;

record FieldInfo(String Name, String? DataType, String? Title, String? Func)
{
	public static FieldInfo FromExpando(ExpandoObject eo)
	{
		return new FieldInfo(
			Name: eo.Get<String>("Name")
				?? throw new InvalidOperationException("Field.Name not found"),
			DataType: eo.Get<String>("DataType"),
			Title: eo.Get<String>("Title") ?? eo.Get<String>("Name"),
			Func: eo.Get<String>("Func")
		);
	}
	public Boolean HasId =>
		DataType switch
		{
			"Period" => false,
			_ => true,
		};
};

public class SheetGeneratorReportInfo : ISheetGenerator
{
	private readonly Sheet _sheet;
    public SheetGeneratorReportInfo(Sheet sheet)
	{
		_sheet = sheet;
	}
    public void Generate(RenderContext context, String propertyName)
	{
		var dm = context.DataModel;
		if (dm == null)
			return;
		var repInfo = dm.Eval<ExpandoObject>(propertyName) 
			?? throw new XamlException($"Property {propertyName} not found in the root of the data model");

		var fields = repInfo.Get<List<ExpandoObject>>("Fields")
			?? throw new XamlException($"Property 'Fields' not found in the {propertyName}");

		var visibleFields = fields
			.Where(f => f.Get<Boolean>("Checked"))
			.Select(f => FieldInfo.FromExpando(f));

        var grFields = repInfo.Get<List<ExpandoObject>>("Grouping")
            ?? throw new XamlException($"Property 'Grouping' not found in the {propertyName}");
		var groupingFields = grFields.Where(f => f.Get<Boolean>("Checked"))
			.Select(f => FieldInfo.FromExpando(f));

		var filtersFields = repInfo.Get<List<ExpandoObject>>("Filters")
                ?? throw new XamlException($"Property 'Filters' not found in the {propertyName}");
		var filters = filtersFields
			.Where(f => f.Get<Boolean>("Checked"))
			.Select(f => FieldInfo.FromExpando(f));

		// columns
		var hasGroupCell = groupingFields.Count() > 1;
		var headerColSpan = 3;
		if (hasGroupCell)
		{
            var col = new SheetColumn() { Width = Length.FromString("16px") };
			_sheet.Columns.Add(col);
        }
		if (groupingFields.Any())
		{
			_sheet.Columns.Add(new SheetColumn() { Width = Length.FromString("4rem") });
			if (hasGroupCell)
				_sheet.Columns.Add(new SheetColumn() { Width = Length.FromString("100%"), MinWidth = Length.FromString("Auto") });
			else
			{
                _sheet.Columns.Add(new SheetColumn() { Width = Length.FromString("Auto") });
                _sheet.Columns.Add(new SheetColumn() { Width = Length.FromString("100%"), MinWidth = Length.FromString("Auto") });
            }
        }
        foreach (var f in visibleFields) {
			var col = new SheetColumn();
			switch (f.DataType)
			{
				case "Currency":
				case "Number":
				case "Date":
				case "DateTime":
					col.Fit = true;
					break;
			}

            _sheet.Columns.Add(col);
        }

        // header section
        {
            var titleRow = new SheetRow() { Style = RowStyle.Title };
			var titleCell = new SheetCell() { ColSpan = headerColSpan + visibleFields.Count() };
			titleCell.SetBinding(nameof(titleCell.Content), new Bind("RepInfo.Title"));
			titleRow.Cells.Add(titleCell);
			_sheet.Header.Add(titleRow);

			// Parameters
            foreach (var filter in filters)
                _sheet.Header.Add(CreateParamRow(filter));

			_sheet.Header.Add(new SheetRow() { Style = RowStyle.Divider });

            var headerTitle = String.Join(" / ", groupingFields.Select(f => f.Title));
            var headerRow = new SheetRow() { Style = RowStyle.Header };
			var cell = new SheetCell() { Content = headerTitle, ColSpan = headerColSpan, VAlign = VerticalAlign.Middle };
			headerRow.Cells.Add(cell);
			foreach (var field in visibleFields)
			{
				headerRow.Cells.Add(CreateHeaderCellFromData(context, field));
			}
			_sheet.Header.Add(headerRow);
        }

        // total section
        {
			var totalSect = new SheetSection();
			var totalRow = new SheetRow() { Style = RowStyle.Total };
			totalSect.Children.Add(totalRow);
			var cell = new SheetCell() { ColSpan = headerColSpan, Content = context.Localize("@[Total]") };	
			totalRow.Cells.Add(cell);
			foreach (var field in visibleFields)
			{
				totalRow.Cells.Add(CreateTotalCellFromData(field));
			}
			_sheet.Sections.Add(totalSect);
		}

		// tree section
		{
			var dataSect = new SheetTreeSection();
			dataSect.SetBinding(nameof(dataSect.ItemsSource), new Bind("RepData.Items"));
			var dataRow = new SheetRow();
			Int32 nameColSpan = 3;
			if (hasGroupCell)
			{
				dataRow.Cells.Add(new SheetGroupCell());
				nameColSpan = 2;
			}
			var cell = new SheetCell() { ColSpan = nameColSpan, GroupIndent = true };
			cell.SetBinding(nameof(cell.Content), new Bind("$groupName") { DataType = DataType.String, Format = "ToString"});
			dataRow.Cells.Add(cell);
			foreach (var field in visibleFields)
			{
				dataRow.Cells.Add(CreateDataCellFromData(field));
			}
			dataSect.Children.Add(dataRow);

			_sheet.Sections.Add(dataSect);
			_sheet.EndInit();
		}
	}

	SheetRow CreateParamRow(FieldInfo field)
	{
        var row = new SheetRow
        {
            Style = RowStyle.Parameter
        };
        var title = new SheetCell
        {
            ColSpan = 2,
            Content = field.Title
        };
        row.Cells.Add(title);

        var val = new SheetCell
        {
            ColSpan = 3
        };

        // TODO: always name?
        var bind = new Bind($"Filter.{field.Name}.Name");
		val.SetBinding(nameof(val.Content), bind);

		if (field.HasId)
			row.SetBinding("If", new Bind($"Filter.{field.Name}.Id"));

		row.Cells.Add(val);	
		return row;
	}
	static SheetCell CreateHeaderCellFromData(RenderContext context, FieldInfo field)
	{
		return new SheetCell
		{
			Align = TextAlign.Center,
			VAlign = VerticalAlign.Middle,
			Content = context.Localize(field.Title)
		};
	}

	static TextAlign GetAlignFromData(FieldInfo field)
	{
		return field.DataType switch
		{
			"Number" or "Currency" => TextAlign.Right,
			"Date" or "DateTime" => TextAlign.Center,
			_ => TextAlign.Left,
		};
	}

    static WrapMode GetWrapFromData(FieldInfo field)
    {
        return field.DataType switch
        {
            "Number" or "Currency" or "Date" or "DateTime" => WrapMode.NoWrap,
            _ => WrapMode.Default
        };
    }

    static Bind GetBindFromData(FieldInfo field, String name)
	{
		var bind = new Bind(name);
		switch (field.DataType)
		{
			case "Number":
				bind.DataType = DataType.Number;
				bind.HideZeros = true;
				break;
			case "Currency":
				bind.DataType = DataType.Currency;
				bind.HideZeros = true;
				break;
			case "Date":
				bind.DataType = DataType.Date;
				break;
		}
		return bind;
	}

	static SheetCell CreateTotalCellFromData(FieldInfo field)
	{
		var dCell = new SheetCell() { Align = GetAlignFromData(field), Wrap = GetWrapFromData(field) };
		dCell.SetBinding(nameof(dCell.Content), GetBindFromData(field, $"RepData.{field.Name}"));
		return dCell;
	}

	static SheetCell CreateDataCellFromData(FieldInfo field)
	{
		var dCell = new SheetCell() { Align = GetAlignFromData(field), Wrap = GetWrapFromData(field) };
		dCell.SetBinding(nameof(dCell.Content), GetBindFromData(field, field.Name));
		return dCell;
	}

    public void ApplySheetPageProps(RenderContext context, SheetPage page, String propertyName)
    {
        var dm = context.DataModel;
        if (dm == null)
            return;
        var repInfo = dm.Eval<ExpandoObject>(propertyName)
            ?? throw new XamlException($"Property {propertyName} not found in the root of the data model");
        var landscape = repInfo.Eval<Boolean>("Landscape");
        if (landscape)
        {
            if (page != null)
                page.Orientation = PageOrientation.Landscape;
        }
    }
}
