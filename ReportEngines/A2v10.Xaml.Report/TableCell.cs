// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

[ContentProperty("Content")]
public class TableCell : ContentElement
{
	public UInt32 ColSpan { get; init; }
	public UInt32 RowSpan { get; init; }

	public override void ApplyStyles(string selector, StyleBag styles)
	{
		var sel = selector + ">Cell";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		ApplyStylesSelf();
		if (Content is XamlElement contXaml)
		{
			contXaml.ApplyStyles("", styles);
		}
	}
}

public class TableCellCollection : List<TableCell>
{

}
