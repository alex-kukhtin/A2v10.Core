using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Web.Services
{
	public interface IModelAction
	{
		public String CurrentSource { get; }
		public String LoadProcedure { get; }
	}

	public interface IModelDefinitionService
	{
		public Task<IModelAction> CreateFromBaseUrlAsync(String url);
	}
}
