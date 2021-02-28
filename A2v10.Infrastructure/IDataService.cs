
using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IDataService
	{
		Task<String> Reload(String baseUrl, Action<ExpandoObject> setParams);
		Task<String> LoadLazy(String baseUrl, Object Id, String propertyName, Action<ExpandoObject> setParams);
		Task<String> Expand(String baseUrl, Object id, Action<ExpandoObject> setParams);
		Task<String> Save(String baseUrl, ExpandoObject data, Action<ExpandoObject> setParams);
	}
}
