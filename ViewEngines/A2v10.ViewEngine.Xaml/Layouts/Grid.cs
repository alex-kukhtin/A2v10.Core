// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Xaml;

public enum AutoFlowMode
{
	Default,
	Row,
	Column,
	RowDense,
	ColumnDense
}

[AttachedProperties("Col,Row,ColSpan,RowSpan,VAlign")]
public class Grid(IServiceProvider serviceProvider) : Container
{

	private readonly IAttachedPropertyManager _attachedPropertyManager = serviceProvider.GetRequiredService<IAttachedPropertyManager>();

    #region Attached Properties

    public Int32? GetCol(Object obj)
	{
		return _attachedPropertyManager.GetProperty<Int32?>("Grid.Col", obj);
	}

	public Int32? GetRow(Object obj)
	{
		return _attachedPropertyManager.GetProperty<Int32?>("Grid.Row", obj);
	}

	public Int32? GetColSpan(Object obj)
	{
		return _attachedPropertyManager.GetProperty<Int32?>("Grid.ColSpan", obj);
	}

	public Int32? GetRowSpan(Object obj)
	{
		return _attachedPropertyManager.GetProperty<Int32?>("Grid.RowSpan", obj);
	}

	public AlignItem? GetVAlign(Object obj)
	{
		return _attachedPropertyManager.GetProperty<AlignItem?>("Grid.VAlign", obj);
	}

	#endregion

	public void SetVAlign(Object obj, AlignItem align)
	{
		_attachedPropertyManager.SetProperty("Grid.VAlign", obj, align);
	}
	public void SetRow(Object obj, Int32 row)
	{
		_attachedPropertyManager.SetProperty("Grid.Row", obj, row);
	}
	public void SetCol(Object obj, Int32 col)
	{
		_attachedPropertyManager.SetProperty("Grid.Col", obj, col);
	}
	public void SetColSpan(Object obj, Int32 colSpan)
	{
		_attachedPropertyManager.SetProperty("Grid.ColSpan", obj, colSpan);
	}

	public void SetRowSpan(Object obj, Int32 rowSpan)
	{
		_attachedPropertyManager.SetProperty("Grid.RowSpan", obj, rowSpan);
	}

	public Length? Height { get; set; }
	public BackgroundStyle Background { get; set; }
	public ShadowStyle DropShadow { get; set; }
	public AutoFlowMode AutoFlow { get; set; }
	public AlignItem AlignItems { get; set; }
	public GapSize? Gap { get; set; }
	public Length? MinWidth { get; set; }
	public Length? Width { get; set; }

	public Overflow? Overflow { get; set; }

	RowDefinitions? _rows;
	ColumnDefinitions? _columns;

	public RowDefinitions Rows
	{
		get
		{
			_rows ??= [];
			return _rows;
		}
		set
		{
			_rows = value;
		}
	}

	public ColumnDefinitions Columns
	{
		get
		{
			_columns ??= [];
			return _columns;
		}
		set
		{
			_columns = value;
		}
	}

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		if (SkipRender(context))
			return;
		var grid = new TagBuilder("div", "grid", IsInGrid);
		onRender?.Invoke(grid);
		MergeAttributes(grid, context);
		grid.AddCssClass(Overflow.ToClass());
		if (Height != null)
			grid.MergeStyle("height", Height.Value);
		if (MinWidth != null)
			grid.MergeStyle("min-width", MinWidth.ToString());
		if (Width != null)
			grid.MergeStyle("width", Width.ToString());
		if (_rows != null)
			grid.MergeStyle("grid-template-rows", _rows.ToAttribute());
		if (_columns != null)
			grid.MergeStyle("grid-template-columns", _columns.ToAttribute());
		if (Background != BackgroundStyle.Default)
			grid.AddCssClass("background-" + Background.ToString().ToKebabCase());
		if (DropShadow != ShadowStyle.None)
		{
			grid.AddCssClass("drop-shadow");
			grid.AddCssClass(DropShadow.ToString().ToLowerInvariant());
		}
		if (Gap != null)
			grid.MergeStyle("grid-gap", Gap.ToString());

		if (AutoFlow != AutoFlowMode.Default)
			grid.MergeStyle("grid-auto-flow", AutoFlow.ToString().ToKebabCase(delim: " "));

		if (AlignItems != AlignItem.Default)
		{
			String aiStyle = AlignItems.ToString().ToLowerInvariant();
			if (AlignItems == AlignItem.Top)
				aiStyle = "start";
			if (AlignItems == AlignItem.Bottom)
				aiStyle = "end";
			grid.MergeStyle("align-items", aiStyle);
		}

		grid.RenderStart(context);
		RenderChildren(context);
		grid.RenderEnd(context);
	}

	public override void RenderChildren(RenderContext context, Action<TagBuilder>? onRenderStatic = null)
	{
		foreach (var ch in Children)
		{
			ch.IsInGrid = true;
			if (ch is GridGroup gg)
			{
				var grpTemplate = new TagBuilder("template");
				gg.MergeAttributes(grpTemplate, context, MergeAttrMode.Visibility);
				grpTemplate.RenderStart(context);
				foreach (var gc in gg.Children)
				{
					gc.IsInGrid = true;
					using (context.GridContext(GetRow(gc), GetCol(gc), GetRowSpan(gc), GetColSpan(gc), GetVAlign(gc)))
					{
						gc.RenderElement(context);
					}
				}
				grpTemplate.RenderEnd(context);
			}
			else
			{
				using (context.GridContext(GetRow(ch), GetCol(ch), GetRowSpan(ch), GetColSpan(ch), GetVAlign(ch)))
				{
					ch.RenderElement(context);
				}
			}
		}
	}

	protected override void OnEndInit()
	{
		base.OnEndInit();
		EndInitAttached(_attachedPropertyManager);
	}
}

public class RowDefinition
{
	public GridLength? Height { get; set; }
}

[TypeConverter(typeof(RowDefinitionsConverter))]
public class RowDefinitions : List<RowDefinition>
{
	public static RowDefinitions FromString(String val)
	{
		var coll = new RowDefinitions();
		foreach (var row in val.Split(','))
		{
			var rd = new RowDefinition
			{
				Height = GridLength.FromString(row.Trim())
			};
			coll.Add(rd);
		}
		return coll;
	}
	public String ToAttribute()
	{
		var sb = new StringBuilder();
		foreach (var w in this)
		{
			sb.Append(w.Height?.Value).Append(' ');
		}
		return sb.ToString();
	}
}

public class ColumnDefinition
{
	public GridLength? Width { get; set; }
}

[TypeConverter(typeof(ColumnDefinitionsConverter))]
public partial class ColumnDefinitions : List<ColumnDefinition>
{

	const String REPEAT_PATTERN = @"^Repeat\((.+)\)$";
#if NET7_0_OR_GREATER
	[GeneratedRegex(REPEAT_PATTERN, RegexOptions.None, "en-US")]
	private static partial Regex RepeatRegex();
#else
	private static Regex REPEAT_REGEX => new(REPEAT_PATTERN, RegexOptions.Compiled);
	private static Regex RepeatRegex() => REPEAT_REGEX;
#endif

	public static ColumnDefinitions FromString(String val)
	{
		var coll = new ColumnDefinitions();
		if (val.StartsWith("Repeat"))
		{
			var match = RepeatRegex().Match(val.Trim());
			if (match.Groups.Count < 2)
				throw new XamlException($"Invalid repeat value '{val}'");

			var w = GridLength.FromString(match.Groups[1].Value.Trim());
			var cd = new ColumnDefinition()
			{
				Width = new GridLength($"repeat(auto-fit, {w})")
			};
			coll.Add(cd);
		}
		else
		{
			foreach (var row in val.Split(','))
			{
				var cd = new ColumnDefinition()
				{
					Width = GridLength.FromString(row.Trim())
				};
				coll.Add(cd);
			}
		}
		return coll;
	}

	public String ToAttribute()
	{
		var sb = new StringBuilder();
		foreach (var w in this)
		{
			sb.Append(w?.Width?.Value).Append(' ');
		}
		return sb.ToString();
	}
}

public class RowDefinitionsConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		else if (sourceType == typeof(RowDefinitions))
			return true;
		return false;
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is RowDefinitions)
			return value;
		else if (value is String strValue)
			return RowDefinitions.FromString(strValue);
		return base.ConvertFrom(context, culture, value);
	}
}

public class ColumnDefinitionsConverter : TypeConverter
{
	public override Boolean CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(String))
			return true;
		else if (sourceType == typeof(ColumnDefinitions))
			return true;
		return false;
	}

	public override Object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, Object value)
	{
		if (value == null)
			return null;
		if (value is ColumnDefinitions)
			return value;
		else if (value is String strVal)
			return ColumnDefinitions.FromString(strVal);
		return base.ConvertFrom(context, culture, value);
	}
}
