// Copyright © 2015-2017 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Infrastructure;

public interface IServiceLocator
{
    IServiceProvider ServiceProvider { get; }

    T GetService<T>() where T : class;

    T GetService<T>(Func<IServiceLocator, T> func) where T : class;

    Object GetService(Type type);

    T? GetServiceOrNull<T>() where T : class;

    void RegisterSingleton<T>(T service) where T : class;

    Boolean IsServiceRegistered<T>() where T : class;

    void Stop();
}
