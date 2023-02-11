// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.Net.Http;
using System.Security.Cryptography;

using Microsoft.Extensions.DependencyInjection;

using Jint;
using Jint.Native;

using A2v10.Data.Interfaces;

namespace A2v10.Services.Javascript;
public class ScriptEnvironment
{
	private readonly IDbContext _dbContext;
	private readonly ScriptConfig _config;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly Engine _engine;

	private String _currentPath = String.Empty;
	public ScriptEnvironment(Engine engine, IServiceProvider serviceProvider)
	{
		_dbContext = serviceProvider.GetRequiredService<IDbContext>();
		_config = new ScriptConfig(serviceProvider.GetRequiredService<IApplicationHost>());
		_httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
		_appCodeProvider = serviceProvider.GetRequiredService<IAppCodeProvider>();
		_engine = engine;
	}

	public void SetPath(String path)
	{
		_currentPath = path;
	}

#pragma warning disable IDE1006 // Naming Styles
	public ScriptConfig config => _config;
#pragma warning restore IDE1006 // Naming Styles

#pragma warning disable IDE1006 // Naming Styles
	public ExpandoObject loadModel(ExpandoObject prms)
#pragma warning restore IDE1006 // Naming Styles
	{
		String? source = prms.Get<String>("source");
		String command = prms.GetNotNull<String>("procedure");
		ExpandoObject? dmParams = prms.Get<ExpandoObject>("parameters");
		var dm = _dbContext.LoadModel(source, command, dmParams);
		return dm.Root;
	}

#pragma warning disable IDE1006 // Naming Styles
	public ExpandoObject saveModel(ExpandoObject prms)
#pragma warning restore IDE1006 // Naming Styles
	{
		String? source = prms.Get<String>("source");
		String command = prms.GetNotNull<String>("procedure");
		ExpandoObject data = prms.GetNotNull<ExpandoObject>("data");
		ExpandoObject? dmParams = prms.Get<ExpandoObject>("parameters");
		var dm = _dbContext.SaveModel(source, command, data, dmParams);
		return dm.Root;
	}

#pragma warning disable IDE1006 // Naming Styles
	public ExpandoObject? executeSql(ExpandoObject prms)
#pragma warning restore IDE1006 // Naming Styles
	{
		String? source = prms.Get<String>("source");
		String command = prms.GetNotNull<String>("procedure");
		ExpandoObject? dmParams = prms.Get<ExpandoObject>("parameters");
		return _dbContext.ReadExpando(source, command, dmParams);
	}

#pragma warning disable IDE1006 // Naming Styles
	public FetchResponse fetch(String url)
#pragma warning restore IDE1006 // Naming Styles
	{
		return fetch(url, null);
	}

#pragma warning disable IDE1006 // Naming Styles
	public FetchResponse fetch(String url, ExpandoObject? prms)
#pragma warning restore IDE1006 // Naming Styles
	{
		return FetchCommand.Execute(_httpClientFactory, url, prms);
	}

#pragma warning disable IDE1006 // Naming Styles
	public FetchResponse invokeCommand(String cmd, String baseUrl, ExpandoObject parameters)
#pragma warning restore IDE1006 // Naming Styles
	{
		throw new NotImplementedException();
	}

#pragma warning disable IDE1006 // Naming Styles
	public String generateApiKey()
#pragma warning restore IDE1006 // Naming Styles
	{
		Int32 size = 48;
		Byte[] data = RandomNumberGenerator.GetBytes(size);
		String res = Convert.ToBase64String(data);
		res = res.Remove(res.Length - 2);
		return res;
	}

#pragma warning disable IDE1006 // Naming Styles
	public String toBase64(String source, int codePage, bool safe)
#pragma warning restore IDE1006 // Naming Styles
	{
		var enc = Encoding.GetEncoding(codePage, new EncoderReplacementFallback(String.Empty), DecoderFallback.ReplacementFallback);
		var bytes = enc.GetBytes(source);
		var res = Convert.ToBase64String(bytes);
		if (safe)
			res = res.Replace('+', '-').Replace('/', '_').TrimEnd('=');
		return res;
	}

#pragma warning disable IDE1006 // Naming Styles
	public JsValue require(String fileName, ExpandoObject prms, ExpandoObject args)
#pragma warning restore IDE1006 // Naming Styles
	{
		var script = _appCodeProvider.ReadTextFile(_currentPath, fileName, false);

		String code = $@"
return (function() {{
const module = {{exports:null }};
{script};
const __exp__ = module.exports;
return function(_this, prms, args) {{
	return __exp__.call(_this, prms, args);
}};
}})();";
		var func = _engine.Evaluate(code);
		return _engine.Invoke(func, this, prms, args);
	}
}


