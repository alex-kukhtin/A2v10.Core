// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using A2v10.System.Xaml;
using System;

namespace A2v10.Xaml.Report;


[ContentProperty("Content")]
public class ContentElement : XamlElement	
{
	public Object? Content { get; init; }
}


