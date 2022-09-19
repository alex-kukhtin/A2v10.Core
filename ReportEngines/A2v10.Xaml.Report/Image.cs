// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report;

public class Image : FlowElement
{
	public Object? Source { get; set; }
	public Length? Width { get; set; }
	public Length? Height { get; set; }
	public String? FileName { get; set; }
}

