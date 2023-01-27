using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace App.Catalog;

public class ServerResult
{

}

public class Item
{
	public Item(IServiceProvider serviceProvider)
	{

	}

	public Task<ServerResult> Method1(Int64 TenantId, Int64 UserId, ExpandoObject prms)
	{
		throw new NotImplementedException();
	}
}
