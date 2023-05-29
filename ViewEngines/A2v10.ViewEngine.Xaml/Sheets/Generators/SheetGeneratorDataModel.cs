// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using A2v10.Data.Interfaces;

namespace A2v10.Xaml;

public class SheetGeneratorDataModel : ISheetGenerator
{
	protected readonly Sheet _sheet;
    public SheetGeneratorDataModel(Sheet sheet)
	{
        _sheet = sheet;
    }
	public void Generate(RenderContext context, String propertyName)
	{
		var dm = context.DataModel;
		if (dm == null)
			return;

		var rootMd = dm.Metadata["TRoot"];
		if (!rootMd.Fields.ContainsKey(propertyName))
			throw new XamlException($"Property {propertyName} not found in the root of the data model");
		var fieldData = rootMd.Fields[propertyName];
		var fieldsMD = dm.Metadata[fieldData.RefObject];

		var header = new SheetRow() { Style = RowStyle.Header };
		var dataRow = new SheetRow();
		_sheet.Header.Add(header);

		var dataSect = new SheetSection();
		dataSect.SetBinding(nameof(dataSect.ItemsSource), new Bind(propertyName));
		dataSect.Children.Add(dataRow);
        _sheet.Sections.Add(dataSect);

		var cols = new SheetColumnCollection();
        _sheet.Columns = cols;

		foreach (var field in fieldsMD.Fields)
		{
			cols.Add(new SheetColumn("Auto"));
			header.Cells.Add(new SheetCell()
			{
				Content = field.Key
			});
			var cellBind = new Bind(field.Key);
			cellBind.SetWrapped();
			var cell = new SheetCell();
			cell.SetBinding(nameof(cell.Content), cellBind);
			switch (field.Value.SqlDataType)
			{
				case SqlDataType.DateTime:
					cellBind.DataType = DataType.DateTime;
					cell.Wrap = WrapMode.NoWrap;
					cell.Align = TextAlign.Center;
					break;
				case SqlDataType.Date:
					cellBind.DataType = DataType.Date;
					cell.Wrap = WrapMode.NoWrap;
					cell.Align = TextAlign.Center;
					break;
				case SqlDataType.Time:
					cellBind.DataType = DataType.Time;
					cell.Wrap = WrapMode.NoWrap;
					cell.Align = TextAlign.Center;
					break;
				case SqlDataType.Currency:
					cellBind.DataType = DataType.Currency;
					cell.Wrap = WrapMode.NoWrap;
					cell.Align = TextAlign.Right;
					break;
				case SqlDataType.Float:
				case SqlDataType.Decimal:
					cellBind.DataType = DataType.Number;
					cell.Wrap = WrapMode.NoWrap;
					cell.Align = TextAlign.Right;
					break;
				case SqlDataType.Int:
				case SqlDataType.Bigint:
					cell.Align = TextAlign.Right;
					cell.Wrap = WrapMode.NoWrap;
					break;
			}
			dataRow.Cells.Add(cell);
		}
	}
}
