// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report;

public sealed class XamlException : Exception
{
	public XamlException(String msg)
		: base(msg)
	{
	}
}
