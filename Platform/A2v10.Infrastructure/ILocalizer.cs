// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Infrastructure;
public interface ILocalizer
{
	String? Localize(String? locale, String? content, Boolean replaceNewLine = true);
	String? Localize(String? content);
	String? this[String? index] { get; }
	IDictionary<String, String> Dictionary { get; }	
}

