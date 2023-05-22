// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Runtime.Interfaces;

namespace A2v10.Platform.Web;
public record InvokeEngineDescriptor(String Name, Type EngineType, InvokeScope Scope);

public class InvokeEngineFactory
{
	private readonly IList<InvokeEngineDescriptor> _list = new List<InvokeEngineDescriptor>();

	public IList<InvokeEngineDescriptor> Engines => _list;

	public void RegisterEngine<T>(String name, InvokeScope scope)
	{
		_list.Add(new InvokeEngineDescriptor(name, typeof(T), scope));
	}
}

public class WebInvokeEngineProvider : IInvokeEngineProvider
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IList<InvokeEngineDescriptor> _engines;

	public WebInvokeEngineProvider(IServiceProvider serviceProvider, IList<InvokeEngineDescriptor> engines)
	{
		_serviceProvider = serviceProvider;
		_engines = engines;
	}

	public IList<InvokeEngineDescriptor> Engines => _engines;

	#region IInvokeEngineProvider
	public IRuntimeInvokeTarget FindEngine(String name)
	{
		var engine = Engines.FirstOrDefault(x => x.Name == name) 
			?? throw new InvalidReqestExecption($"Invoke engine not found for '{name}' not found");
        return _serviceProvider.GetRequiredService(engine.EngineType) as IRuntimeInvokeTarget ?? 
			throw new InvalidProgramException($"Service {engine.EngineType} is not an IRuntimeInvokeTarget");
	}
	#endregion

	public void RegisterEngine(String name, Type engineType, InvokeScope scope)
	{
		if (!engineType.IsAssignableTo(typeof(IRuntimeInvokeTarget)))
			throw new InvalidProgramException($"Type {engineType} does not implement IRuntimeInvokeTarget");
		Engines.Add(new InvokeEngineDescriptor(name, engineType, scope));
	}
}

