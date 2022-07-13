// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Xaml.Report;

public interface ISupportBinding
{
	BindImpl BindImpl { get; }
	Bind? GetBinding(String name);
}
