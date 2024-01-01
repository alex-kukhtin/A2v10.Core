using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace App.Catalog;

public class ServerResult
{

}

#pragma warning disable CS9113 // Parameter is unread.
public class Item(IServiceProvider _)
#pragma warning restore CS9113 // Parameter is unread.
{
    public Task<ServerResult> Method1(Int64 TenantId, Int64 UserId, ExpandoObject prms)
	{
		throw new NotImplementedException();
	}
}
