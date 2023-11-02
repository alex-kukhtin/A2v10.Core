// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.


namespace A2v10.Services;

public static class ParameterBuilder
{
	public static class WellKnownParamNames
	{
		public const String TenantId = "TenantId";
		public const String UserId = "UserId";
		public const String CompanyId = "CompanyId";
	}

	public static ExpandoObject BuildIndirectParams(IPlatformUrl url, Action<ExpandoObject> setParams)
	{
		var result = new ExpandoObject();
		result.SetNotNull("Id", url.Id);
		setParams?.Invoke(result);
		return result;
	}
}
