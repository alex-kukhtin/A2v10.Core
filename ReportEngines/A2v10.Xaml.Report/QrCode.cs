// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

[ContentProperty("Value")]
public class QrCode : FlowElement
{
	public Object? Value { get; set; }
	public Length? Size { get; set; }
}
