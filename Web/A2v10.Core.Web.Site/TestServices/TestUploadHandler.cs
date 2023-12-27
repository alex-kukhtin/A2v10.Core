using A2v10.Infrastructure;
using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Core.Web.Site;

public class TestUploadHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
	public Task<object> InvokeAsync(ExpandoObject args)
	{
		var blobObj = args.Get<Object>("Blob");
		if (blobObj is not IBlobUpdateInfo blobUpdateInfo)
			throw new InvalidOperationException("Invalid blob args");
		if (blobUpdateInfo.Stream == null)
			throw new InvalidOperationException("Steam is null");
		throw new NotImplementedException();
	}
}
