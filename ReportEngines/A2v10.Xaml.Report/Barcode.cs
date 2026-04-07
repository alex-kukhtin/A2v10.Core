// Copyright © 2024-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

public enum BarcodeType
{
    EAN13,
    EAN8
}

[ContentProperty("Value")]
public class Barcode : FlowElement
{
	public Object? Value { get; init; }
	public Length? Width { get; init; }
    public Int32 Height { get; init; }
    public BarcodeType Type { get; init; }
    public Boolean PrintDigits { get; init; } = true;
}
