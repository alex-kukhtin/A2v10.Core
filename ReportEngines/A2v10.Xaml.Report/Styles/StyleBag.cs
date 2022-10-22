// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report;

public class StyleBag
{
	public Dictionary<String, RuntimeStyle> _styles = new();

	const String THIN_BORDER = ".2pt";
	public StyleBag()
	{

		_styles.Add("Page", new RuntimeStyle()
		{
			FontSize = 9F,
			Margin = Thickness.FromString("20mm,10mm")
		}); 

		_styles.Add("Text.Title", new RuntimeStyle()
		{
			FontSize = 12F,
		});

		_styles.Add("Table.Details", new RuntimeStyle()
		{
			Border = Thickness.FromString("1pt"),
			Margin = Thickness.FromString("10pt,0")
		});

		_styles.Add("Table.Details>Header>Row>Cell", new RuntimeStyle()
		{
			Padding = Thickness.FromString("1pt,4pt"),
			VAlign = VertAlign.Middle,
			Border = Thickness.FromString(THIN_BORDER),
			Background = "#f5f5f5"
		});

		_styles.Add("Table.Details>Body>Row>Cell", new RuntimeStyle()
		{
			Padding = Thickness.FromString("1pt,4pt"),
			Border = Thickness.FromString(THIN_BORDER),
		});

		_styles.Add("Table.Details>Footer>Row>Cell", new RuntimeStyle()
		{
			Padding = Thickness.FromString("1pt,4pt"),
			Border = Thickness.FromString(THIN_BORDER),
			Bold = true
		});

		_styles.Add("Table.Default>Body>Row>Cell", new RuntimeStyle()
		{
			//Padding = Thickness.FromString("1pt,0")
		});


		_styles.Add("Table.Simple>Body>Row>Cell", new RuntimeStyle()
		{
			Padding = Thickness.FromString("1pt,4pt"),
			Border = Thickness.FromString(THIN_BORDER),
		});
		_styles.Add("Table.Simple>Header>Row>Cell", new RuntimeStyle()
		{
			Padding = Thickness.FromString("1pt,4pt"),
			VAlign = VertAlign.Middle,
			Border = Thickness.FromString(THIN_BORDER),
			Background = "#f5f5f5"
		});

	}

	public RuntimeStyle? GetRuntimeStyle(String selector)
	{
		return FindStyles(selector);
	}

	RuntimeStyle? FindStyles(String selector) 
	{ 
		foreach (var key in _styles.Keys)
		{
			if (selector.EndsWith(key))
			{
				return _styles[key].Clone();
			}
		}
		return null;
	}
}
