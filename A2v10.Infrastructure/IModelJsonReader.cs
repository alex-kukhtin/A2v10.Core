// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IModelView
	{
		String DataSource { get; }
		String LoadProcedure();
		String ExpandProcedure();
		String UpdateProcedure();
		String LoadLazyProcedure(String property);

		ExpandoObject Parameters { get; }
	}

	public interface IModelCommand
	{
	}

	public interface IModelJsonReader
	{
		Task<IModelView> GetViewAsync(IPlatformUrl url);
	}
}
