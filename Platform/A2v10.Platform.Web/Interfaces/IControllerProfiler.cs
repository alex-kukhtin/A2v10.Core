// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public interface IControllerProfiler
{
	IProfiler Profiler { get; }
	IProfileRequest? BeginRequest();
	void EndRequest(IProfileRequest? request);
}
