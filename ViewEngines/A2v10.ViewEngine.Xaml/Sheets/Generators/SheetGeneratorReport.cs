// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.Xaml;

record HeaderField(String TopName, String BottomName, Int32 TopSpan, Boolean IsSingle)
{
	public static HeaderField FromString(String val)
	{
		var spl = val.Split('|');
		var topName = spl[0];
		var bottomName = String.Empty;
		Int32 topSpan = 1;
		Int32 pos = topName.LastIndexOf(':');
		if (pos != -1)
		{
			topSpan += Int32.Parse(topName[(pos + 1)..]);
			topName = topName[0..pos];
		};
		if (spl.Length == 2)
			bottomName = spl[1];
		return new HeaderField(topName, bottomName, topSpan, !val.Contains('|'));
	}
	public Boolean IsTop => !String.IsNullOrEmpty(TopName);
	public Boolean IsBottom => !String.IsNullOrEmpty(BottomName);
}

record FieldInfo(String Name, String? DataType, String Title, String? Func, Boolean Wide)
{
	public static FieldInfo FromExpando(ExpandoObject eo)
	{
		return new FieldInfo(
			Name: eo.Get<String>(nameof(Name))
				?? throw new InvalidOperationException("Field.Name not found"),
			DataType: eo.Get<String>(nameof(DataType)),
			Title: eo.Get<String>(nameof(Title)) ?? eo.Get<String>(nameof(Name)) ?? 
				throw new InvalidOperationException("Name and Title is null"),
			Func: eo.Get<String>(nameof(Func)),
			Wide: eo.Get<Boolean>(nameof(Wide))
		);
	}
	public Boolean HasId =>
		DataType switch
		{
			"Period" => false,
			_ => true,
		};
	public Boolean IsCross => Func == "Cross";
	public HeaderField HeaderField => HeaderField.FromString(Title); 
	public Boolean HasTopHeader => Title != null;
	public String CrossCrossName => IsCross ? Name.Split('#')[0] : throw new InvalidOperationException("Invalid cross cross");
	public String CrossFieldName => IsCross ? Name.Split('#')[1] : throw new InvalidOperationException("Invalid cross field");
}

public class SheetGeneratorReportInfo(Sheet _sheet) : ISheetGenerator
{
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

		var colCount = visibleFields.Count(f => f.Wide) + 1;
		var wideColWidth = Length.FromString($"{Math.Round(100.0 / colCount, 2)}%");
		var autoColWidth = Length.FromString("Auto");

		var headerColSpan = 3;
		if (hasGroupCell)
		{
			var col = new SheetColumn() { Width = Length.FromString("16px") };
			_sheet.Columns.Add(col);
		}
		_sheet.Columns.Add(new SheetColumn() { Width = Length.FromString("4rem") });
		if (groupingFields.Any())
		{
			if (hasGroupCell)
				_sheet.Columns.Add(new SheetColumn() { Width = wideColWidth, MinWidth = autoColWidth });
			else
			{
				_sheet.Columns.Add(new SheetColumn() { Width = autoColWidth });
				_sheet.Columns.Add(new SheetColumn() { Width = wideColWidth, MinWidth = autoColWidth });
			}
		}
		else
		{
			_sheet.Columns.Add(new SheetColumn() { Width = autoColWidth });
			_sheet.Columns.Add(new SheetColumn() { Width = wideColWidth, MinWidth = autoColWidth });
		}
		Boolean nextGray = false;
		foreach (var f in visibleFields)
		{
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
			if (f.Wide)
			{
				col.Width = wideColWidth;
				col.MinWidth = autoColWidth;
			}
			if (nextGray)
			{
				col.Background = ColumnBackgroundStyle.Gray;
				nextGray = false;	
			}
			if (f.IsCross)
			{
				var cg = new SheetColumnGroup();
				cg.SetBinding(nameof(SheetColumnGroup.ItemsSource), new Bind($"RepData.Items.$cross.{f.CrossCrossName}.length"));
				nextGray = true;
				cg.Columns.Add(col);
				_sheet.Columns.Add(cg);
			} 
			else
				_sheet.Columns.Add(col);
		}

		var hasCross = visibleFields.Any(f => f.IsCross);

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
			var cell = new SheetCell() { Content = headerTitle, ColSpan = headerColSpan, VAlign = VerticalAlign.Middle, RowSpan = hasCross ? 2 : 1 };
			headerRow.Cells.Add(cell);
			_sheet.Header.Add(headerRow);

			if (hasCross)
			{
				foreach (var field in visibleFields.Where(f => f.HeaderField.IsTop))
				{
					var hf = field.HeaderField;
					var headerCell = CreateHeaderCellFromData(context, hf.TopName);
					if (field.IsCross)
					{
						headerCell.SetBinding(nameof(SheetCell.ColSpan),
							new Bind($"RepData.Items.${field.CrossCrossName}ColSpan"));
					}
					else
					{
						headerCell.ColSpan = hf.TopSpan;
						if (hf.IsSingle)
							headerCell.RowSpan = 2;
					}
					headerRow.Cells.Add(headerCell);
				}
				var headerRow2 = new SheetRow() { Style = RowStyle.Header };
				_sheet.Header.Add(headerRow2);
				foreach (var field in visibleFields)
				{
					var hf = field.HeaderField;
					if (field.IsCross)
					{
						var grp = new SheetCellGroup();
						grp.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind($"RepData.Items.$cross.{field.CrossCrossName}"));
						var grpCell = CreateHeaderCellFromData(context, String.Empty);
						grpCell.SetBinding(nameof(SheetCell.Content), new Bind());
						grp.Cells.Add(grpCell);
						headerRow2.Cells.Add(grp);
					}
					else if (!hf.IsSingle)
					{
						headerRow2.Cells.Add(CreateHeaderCellFromData(context, hf.BottomName));
					}
				}
			}
			else
			{
				foreach (var field in visibleFields)
				{
					headerRow.Cells.Add(CreateHeaderCellFromData(context, field.Title));
				}
			}
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
				if (field.IsCross)
				{
					totalRow.Cells.Add(CreateGroupCellFromData(field, $"RepData.Items.${field.CrossCrossName}Totals", field.CrossFieldName));
				}
				else
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
			cell.SetBinding(nameof(cell.Content), new Bind("$groupName") { DataType = DataType.String, Format = "ToString" });
			dataRow.Cells.Add(cell);
			foreach (var field in visibleFields)
			{
				if (field.IsCross)
					dataRow.Cells.Add(CreateGroupCellFromData(field, field.CrossCrossName, field.CrossFieldName));
				else
					dataRow.Cells.Add(CreateDataCellFromData(field));
			}
			dataSect.Children.Add(dataRow);

			_sheet.Sections.Add(dataSect);
			_sheet.EndInit();
		}
	}

	static SheetRow CreateParamRow(FieldInfo field)
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
	static SheetCell CreateHeaderCellFromData(RenderContext context, String? title)
	{
		return new SheetCell
		{
			Align = TextAlign.Center,
			VAlign = VerticalAlign.Middle,
			Content = context.Localize(title)
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

	static SheetCellGroup CreateGroupCellFromData(FieldInfo field, String path1, String path2)
	{
		var gr = new SheetCellGroup();
		gr.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind(path1));
		var grCell = CreateDataCellFromData(field, path2);
		gr.Cells.Add(grCell);
		return gr;
	}

	static ISheetCell CreateDataCellFromData(FieldInfo field)
	{
		if (field.IsCross)
		{
			var names = field.Name.Split('#');
			var gr = new SheetCellGroup();
			gr.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind(names[0]));
			var cell = CreateDataCellFromData(field, names[1]);
			gr.Cells.Add(cell);
			return gr;
		}
		else
		{
			var dCell = new SheetCell() { Align = GetAlignFromData(field), Wrap = GetWrapFromData(field) };
			dCell.SetBinding(nameof(dCell.Content), GetBindFromData(field, field.Name));
			return dCell;
		}
	}
	static SheetCell CreateDataCellFromData(FieldInfo field, String content)
	{
		var dCell = new SheetCell() { Align = GetAlignFromData(field), Wrap = GetWrapFromData(field) };
		dCell.SetBinding(nameof(dCell.Content), GetBindFromData(field, content));
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
