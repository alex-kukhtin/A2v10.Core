// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.App.Infrastructure;

public class Host
{
    protected readonly IServiceProvider _serviceProvider;
    public Host(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }   
    public T New<T>() where T : ElementBase
    {
        return (T)Activator.CreateInstance(typeof(T), _serviceProvider)!;
    }
}


public class ElementBase
{
    public Host Host; 
    public ElementBase(IServiceProvider serviceProvider)
    {
        Host = new Host(serviceProvider);
    }
}
