
using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Core.Web.Site
{
	public class FakeWorkflow : IRuntimeInvokeTarget
	{
		public Task<ExpandoObject> InvokeAsync(String method, ExpandoObject parameters)
		{
			switch (method) {
				case "Run":
					return Task.FromResult(new ExpandoObject()
					{
						{ "InstanceId", Guid.NewGuid() }
					});
				case "Resume": 
					return Task.FromResult(new ExpandoObject() 
					{
						{ "InstanceId", Guid.NewGuid() },
						{ "Reply", parameters.EvalExpression("reply")}
					});
			}
			throw new InvalidOperationException($"Method {method} not found");
		}
	}
}
