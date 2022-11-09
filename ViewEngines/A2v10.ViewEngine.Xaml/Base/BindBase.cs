// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System.Reflection;


namespace A2v10.Xaml;

public abstract class BindBase : MarkupExtension, ISupportBinding
{
	private BindImpl? _bindImpl;

	public BindImpl BindImpl
	{
		get
		{
			_bindImpl ??= new BindImpl();
			return _bindImpl;
		}
	}

	public override Object? ProvideValue(IServiceProvider serviceProvider)
	{
		if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget iTarget)
			return null;
		if (iTarget.TargetProperty is PropertyInfo targetProp &&
			iTarget.TargetObject is ISupportBinding targetObj)
		{
			targetObj.BindImpl.SetBinding(targetProp.Name, this);
			if (targetProp.PropertyType.IsValueType)
				return Activator.CreateInstance(targetProp.PropertyType);
		}
		return null; // is object
	}

	public Bind? GetBinding(String name)
	{
		return _bindImpl?.GetBinding(name);
	}

	public BindCmd? GetBindingCommand(String name)
	{
		return _bindImpl?.GetBindingCommand(name);
	}
}

