// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	// TODO: to A2v10.Runtime.Interfaces
	public interface IRuntimeInvokeTarget
	{
		Task<ExpandoObject> InvokeAsync(String method, ExpandoObject parameters);
	}
	// end of intrfaces


	public enum InvokeScope
	{
		Singleton,
		Scoped,
		Transient
	}

	public interface IInvokeEngineProvider
	{
		IRuntimeInvokeTarget FindEngine(String name);
	}
}
