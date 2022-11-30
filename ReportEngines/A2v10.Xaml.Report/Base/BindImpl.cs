// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Xaml.Report;

public class BindImpl
{
	IDictionary<String, Bind>? _bindings;

	public Bind SetBinding(String name, Bind bind)
	{
		_bindings ??= new Dictionary<String, Bind>();
		if (_bindings.ContainsKey(name))
			_bindings[name] = bind;
		else
			_bindings.Add(name, bind);
		return bind;
	}

	public void RemoveBinding(String name)
	{
		if (_bindings == null) 
			return;
		if (_bindings.ContainsKey(name))
			_bindings.Remove(name);
	}

	public Bind? GetBinding(String name)
	{
		if (_bindings == null)
			return null;
		if (_bindings.TryGetValue(name, out Bind? bind))
		{
			if (bind is Bind iBind)
				return iBind;
			throw new XamlException($"Binding '{name}' must be a Bind");
		}
		return null;
	}

	public BindRuntime? GetBindRuntime(String name)
	{
		return GetBinding(name)?.Runtime();
	}
}
