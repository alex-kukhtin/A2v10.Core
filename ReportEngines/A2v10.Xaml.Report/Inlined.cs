// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

[ContentProperty("Children")]
public class Inlined : FlowElement
{
	public FlowElementCollection Children { get; set; } = new FlowElementCollection();

	public override void ApplyStyles(String selector, StyleBag styles)
	{
		_runtimeStyle = styles.GetRuntimeStyle(selector);
		ApplyStylesSelf();
		foreach (var ch in Children)
			ch.ApplyStyles(selector, styles);
	}
}
