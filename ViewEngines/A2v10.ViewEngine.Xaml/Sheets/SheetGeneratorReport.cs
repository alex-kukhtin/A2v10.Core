// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

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

	public Boolean IsGroup => Func == "Group";
};

public partial class Sheet
{
	void GenerateFromReportInfo(RenderContext context, String propertyName)
	{
		var dm = context.DataModel;
		if (dm == null)
			return;
		var repInfo = dm.Eval<ExpandoObject>(propertyName) 
			?? throw new XamlException($"Property {propertyName} not found in the root of the data model");

		var fields = repInfo.Get<List<ExpandoObject>>("Fields")
			?? throw new XamlException($"Property 'Fields' not found in the ReportProperty");

		var visibleFields = fields
			.Where(f => f.Get<Boolean>("Checked"))
			.Select(f => FieldInfo.FromExpando(f));

		var drawFields = visibleFields
			.Where(f => !f.IsGroup);

		var groupingFields = visibleFields.Where(f => f.IsGroup)
			.Where(f => f.IsGroup);


		// header section
		{
			var titleRow = new SheetRow() { Style = RowStyle.Title };
			var titleCell = new SheetCell() { ColSpan = 3 + drawFields.Count() };
			titleCell.SetBinding(nameof(titleCell.Content), new Bind("RepInfo.Title"));
			titleRow.Cells.Add(titleCell);
			Header.Add(titleRow);

			// TODO: parameters

			var headerTitle = String.Join("/", groupingFields.Select(f => f.Title));

			var headerRow = new SheetRow() { Style = RowStyle.Header };
			var cell = new SheetCell() { Content = headerTitle, ColSpan = 3 };
			headerRow.Cells.Add(cell);
			foreach (var field in drawFields)
			{
				headerRow.Cells.Add(CreateHeaderCellFromData(context, field));
			}
			Header.Add(headerRow);
		}

		// total section
		{
			var totalSect = new SheetSection();
			var totalRow = new SheetRow() { Style = RowStyle.Total };
			totalSect.Children.Add(totalRow);
			var cell = new SheetCell() { ColSpan = 3, Content = context.Localize("@[Total]") };	
			totalRow.Cells.Add(cell);
			foreach (var field in drawFields)
			{
				totalRow.Cells.Add(CreateTotalCellFromData(field));
			}
			Sections.Add(totalSect);
		}

		// tree section
		{
			var dataSect = new SheetTreeSection();
			dataSect.SetBinding(nameof(dataSect.ItemsSource), new Bind("RepData.Items"));
			var dataRow = new SheetRow();
			dataRow.Cells.Add(new SheetGroupCell());
			var cell = new SheetCell() { ColSpan = 2, GroupIndent = true };
			cell.SetBinding(nameof(cell.Content), new Bind("$groupName") { DataType = DataType.String, Format = "ToString"});
			dataRow.Cells.Add(cell);
			foreach (var field in drawFields)
			{
				dataRow.Cells.Add(CreateDataCellFromData(field));
			}
			dataSect.Children.Add(dataRow);

			Sections.Add(dataSect);
			this.OnEndInit();
		}
	}
	static SheetCell CreateHeaderCellFromData(RenderContext context, FieldInfo field)
	{
		return new SheetCell
		{
			Align = TextAlign.Center,
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
		var dCell = new SheetCell() { Align = GetAlignFromData(field) };
		dCell.SetBinding(nameof(dCell.Content), GetBindFromData(field, $"RepData.{field.Name}"));
		return dCell;
	}

	static SheetCell CreateDataCellFromData(FieldInfo field)
	{
		var dCell = new SheetCell() { Align = GetAlignFromData(field) };
		dCell.SetBinding(nameof(dCell.Content), GetBindFromData(field, field.Name));
		return dCell;
	}
}
