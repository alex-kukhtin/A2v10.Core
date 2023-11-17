// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Xaml;
public class BindImpl
{
	Dictionary<String, BindBase>? _bindings;

	public BindBase SetBinding(String name, BindBase bind)
	{
		_bindings ??= [];
		if (!_bindings.TryAdd(name, bind))
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
		if (_bindings.TryGetValue(name, out BindBase? bind))
		{
			if (bind is Bind asBind)
				return asBind;
			throw new XamlException($"Binding '{name}' must be a Bind");
		}
		return null;
	}

	public BindCmd? GetBindingCommand(String name)
	{
		if (_bindings == null)
			return null;
		if (_bindings.TryGetValue(name, out BindBase? bind))
		{
			if (bind is BindCmd bindCmd)
				return bindCmd;
			throw new XamlException($"Binding '{name}' must be a BindCmd");
		}
		return null;
	}
}

