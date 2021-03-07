// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using A2v10.Infrastructure;

namespace A2v10.Services
{
	public static class ParameterBuilder
	{
		public static class WellKnownParamNames
		{
			public const String TenantId = "TenantId";
			public const String UserId = "UserId";
			public const String CompanyId = "CompanyId";
		}

		public static ExpandoObject BuildParams(IPlatformUrl url, ExpandoObject jsonParams, Action<ExpandoObject> onSetParams)
		{
			// initial: [query, controller]
			var initial = new ExpandoObject();
			initial.Append(url.Query);
			onSetParams?.Invoke(initial);
			// real: [json, id, initial]
			var real = new ExpandoObject();
			real.Append(jsonParams);
			real.SetNotNull("Id", url.Id);
			real.Append(initial);

			return real;
		}

		public static ExpandoObject BuildIndirectParams(IPlatformUrl url, Action<ExpandoObject> setParams)
		{
			var result = new ExpandoObject();
			result.SetNotNull("Id", url.Id);
			setParams?.Invoke(result);
			return result;
		}

		public static ExpandoObject BuildSaveParams(ExpandoObject jsonParams, Action<ExpandoObject> setParams)
		{
			var savePrms = new ExpandoObject();
			setParams?.Invoke(savePrms);
			savePrms.Append(jsonParams);
			return savePrms;
		}
	}
}
