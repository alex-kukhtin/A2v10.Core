// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Reflection;


namespace A2v10.Xaml;

public abstract class BindBase : MarkupExtension, ISupportBinding, IBindWriter
{
	private BindImpl? _bindImpl;

	protected abstract String ClassName { get; }
    public abstract String CreateMarkup();

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


    #region IBindWriter 
    public virtual String? CreateMarkup(String name)
    {
        if (_bindImpl == null)
            return null;
        var bind = _bindImpl.GetBindingBase(name);
        if (bind != null)
            return bind.CreateMarkup();
        return null;
    }
    #endregion
}

