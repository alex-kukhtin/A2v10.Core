// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using A2v10.System.Xaml;
using System;
using System.Reflection;


namespace A2v10.Xaml.Report;

public class Bind : MarkupExtension, ISupportBinding
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

	public Bind()
	{
	}

	public Bind(String path)
	{
		Expression = path;	
	}

	public String? Expression { get; init; }

	public DataType DataType { get; init; }

	public String? Format { get; init; }

	public BindRuntime Runtime()
	{
		return new BindRuntime()
		{
			Expression = this.Expression,
			Format = this.Format,
			DataType = this.DataType
		};
	}

	public override Object? ProvideValue(IServiceProvider serviceProvider)
	{
		if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget iTarget)
			return null;
		if (iTarget.TargetProperty is not PropertyInfo targetProp)
			return null;
		if (iTarget.TargetObject is not ISupportBinding targetObj)
			return null;
		targetObj.BindImpl.SetBinding(targetProp.Name, this);
		if (targetProp.PropertyType.IsValueType)
			return Activator.CreateInstance(targetProp.PropertyType);
		return null; // is object
	}

	public Bind? GetBinding(String name)
	{
		return _bindImpl?.GetBinding(name);
	}
}
