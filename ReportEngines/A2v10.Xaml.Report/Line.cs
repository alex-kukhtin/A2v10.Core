// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report;


public class Line : FlowElement
{
	public Length Thickness { get; set; } = Length.FromString("1pt");

	public override void ApplyStyles(String selector, StyleBag styles)
	{
		ApplyStylesSelf();
	}
}
