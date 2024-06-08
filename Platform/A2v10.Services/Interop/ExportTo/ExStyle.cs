// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Interop;

public enum RowRole
{
	None,
	Title,
	Parameter,
	LastParameter,
	Header,
	LightHeader,
	Total,
	Body,
	Footer,
	Divider
}

public struct Style
{
	public HorizontalAlign Align;
	public VerticalAlign VAlign;
	public Boolean Bold;
	public DataType DataType;
	public Boolean Wrap;
	public RowRole RowRole;
	public RowKind RowKind;
	public UInt32 Indent;
	public Boolean Underline;

	public readonly Boolean HasBorder => RowKind == RowKind.Body || RowRole == RowRole.Header || RowRole == RowRole.Footer || RowRole == RowRole.Total;
	public readonly Boolean IsDateOrTime => DataType == DataType.Date || DataType == DataType.DateTime || DataType == DataType.Time;
	public readonly Boolean IsBoolean => DataType == DataType.Boolean;
}

public class StylesDictionary
{
	private readonly Dictionary<Style, UInt32> _hash = [];
	public List<Style> List { get; } = [];

	public StylesDictionary()
	{
		var d = new Style();
		// default style (zero)
		GetOrCreate(d);
	}

	public UInt32 GetOrCreate(Style style)
	{
		if (_hash.TryGetValue(style, out UInt32 index))
			return index;
		List.Add(style);
		var newIndex = (UInt32)List.Count - 1;
		_hash.Add(style, newIndex);
		return newIndex;
	}
}
