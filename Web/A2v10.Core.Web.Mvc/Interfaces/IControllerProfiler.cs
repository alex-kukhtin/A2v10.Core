using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Core.Web.Mvc
{
	public interface IControllerProfiler
	{
		IProfiler Profiler { get; }
		IProfileRequest BeginRequest();
		void EndRequest(IProfileRequest request);
	}
}
