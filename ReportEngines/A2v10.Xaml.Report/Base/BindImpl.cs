// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report;

public class BindImpl
{
	Dictionary<String, Bind>? _bindings;

	public Bind SetBinding(String name, Bind bind)
	{
		_bindings ??= [];
		if (_bindings.TryAdd(name, bind))
			return bind;
		_bindings[name] = bind;
		return bind;
	}

	public void RemoveBinding(String name)
	{
        _bindings?.Remove(name);
    }

	public Bind? GetBinding(String name)
	{
		if (_bindings == null)
			return null;
		if (_bindings.TryGetValue(name, out Bind? bind))
			return bind;
		return null;
	}

	public BindRuntime? GetBindRuntime(String name)
	{
		return GetBinding(name)?.Runtime();
	}
}
