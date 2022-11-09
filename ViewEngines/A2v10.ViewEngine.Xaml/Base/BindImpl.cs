// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Xaml;
public class BindImpl
{
	IDictionary<String, BindBase>? _bindings;

	public BindBase SetBinding(String name, BindBase bind)
	{
		_bindings ??= new Dictionary<String, BindBase>();
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

