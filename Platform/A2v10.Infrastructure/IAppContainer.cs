// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Infrastructure;

public interface IAppContainer
{
	T? GetModelJson<T>(String path);
	String? GetText(String path);
	Object? GetXamlObject(String path);
	IEnumerable<String> EnumerateFiles(String prefix, String pattern);
}
