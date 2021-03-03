// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IModelBase
	{
		String DataSource { get; }

		String LoadProcedure();
		Boolean HasModel();

		String Path { get; }
		String BaseUrl { get; }
	}

	public interface IModelView: IModelBase
	{
		Boolean Copy { get; }
		String Template { get; }

		Boolean Indirect { get; }
		String Target { get; }
		String TargetId { get; }
		IModelView TargetModel { get; }

		ExpandoObject Parameters { get; }

		IModelBase Merge { get; }

		String GetView(Boolean bMobile);
		Boolean IsDialog { get; }

		String ExpandProcedure();
		String UpdateProcedure();
		String LoadLazyProcedure(String property);
	}

	public interface IModelCommand
	{
	}

	public interface IModelJsonReader
	{
		Task<IModelView> GetViewAsync(IPlatformUrl url);
	}
}
