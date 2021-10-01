
using System;
using System.Dynamic;

using Microsoft.Extensions.DependencyInjection;

using Jint;
using Jint.Native;
using Jint.Runtime;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Services.Javascript
{
	public class ScriptEnvironment
	{
		private readonly IDbContext _dbContext;
		private readonly ScriptConfig _config;

		public ScriptEnvironment(IServiceProvider serviceProvider)
		{
			_dbContext = serviceProvider.GetService<IDbContext>();
			_config = new ScriptConfig(serviceProvider.GetService<IApplicationHost>());
		}

#pragma warning disable IDE1006 // Naming Styles
		public ScriptConfig config => _config;
#pragma warning restore IDE1006 // Naming Styles

#pragma warning disable IDE1006 // Naming Styles
		public ExpandoObject loadModel(ExpandoObject prms)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				String source = prms.Get<String>("source");
				String command = prms.Get<String>("procedure");
				ExpandoObject dmParams = prms.Get<ExpandoObject>("parameters");
				var dm = _dbContext.LoadModel(source, command, dmParams);
				return dm.Root;
			}
			catch (Exception ex)
			{
				if (ex.InnerException != null)
					ex = ex.InnerException;
				var js = new JsString(ex.Message);
				throw new JavaScriptException(js);
			}
		}

#pragma warning disable IDE1006 // Naming Styles
		public ExpandoObject saveModel(ExpandoObject prms)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				String source = prms.Get<String>("source");
				String command = prms.Get<String>("procedure");
				ExpandoObject data = prms.Get<ExpandoObject>("data");
				ExpandoObject dmParams = prms.Get<ExpandoObject>("parameters");
				var dm = _dbContext.SaveModel(source, command, data, dmParams);
				return dm.Root;
			}
			catch (Exception ex)
			{
				if (ex.InnerException != null)
					ex = ex.InnerException;
				var js = new JsString(ex.Message);
				throw new JavaScriptException(js);
			}
		}

#pragma warning disable IDE1006 // Naming Styles
		public ExpandoObject executeSql(ExpandoObject prms)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				String source = prms.Get<String>("source");
				String command = prms.Get<String>("procedure");
				ExpandoObject dmParams = prms.Get<ExpandoObject>("parameters");
				return _dbContext.ReadExpando(source, command, dmParams);
			}
			catch (Exception ex)
			{
				if (ex.InnerException != null)
					ex = ex.InnerException;
				var js = new JsString(ex.Message);
				throw new JavaScriptException(js);
			}
		}

#pragma warning disable IDE1006 // Naming Styles
		public FetchResponse fetch(String url)
#pragma warning restore IDE1006 // Naming Styles
		{
			return fetch(url, null);
		}

#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CA1822 // Naming Styles
		public FetchResponse fetch(String url, ExpandoObject prms)
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CA1822 // Naming Styles
		{
			try
			{
				return FetchCommand.Execute(url, prms);
			}
			catch (Exception ex)
			{
				if (ex.InnerException != null)
					ex = ex.InnerException;
				var js = new JsString(ex.Message);
				throw new JavaScriptException(js);
			}
		}
	}
}
