using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Core.Web.Site;

#pragma warning disable CS9113 // Parameter is unread.
public class TestUploadHandler(IServiceProvider _) : IClrInvokeTarget
#pragma warning restore CS9113 // Parameter is unread.
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
