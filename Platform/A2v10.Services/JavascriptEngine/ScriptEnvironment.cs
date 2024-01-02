// Copyright © 2021-2024 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

using Microsoft.Extensions.DependencyInjection;

using Jint;
using Jint.Native;

using A2v10.Data.Interfaces;

namespace A2v10.Services.Javascript;

#pragma warning disable IDE1006 // Naming Styles

public class ScriptEnvironment
{
	private readonly IDbContext _dbContext;
	private readonly ScriptConfig _config;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly ICurrentUser _currentUser;
	private readonly Engine _engine;
	private readonly ScriptUser _currentScriptUser;

	//private String _currentPath = String.Empty;
	public ScriptEnvironment(Engine engine, IServiceProvider serviceProvider)
	{
		_dbContext = serviceProvider.GetRequiredService<IDbContext>();
		_config = new ScriptConfig(serviceProvider.GetRequiredService<IApplicationHost>());
		_httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
		_appCodeProvider = serviceProvider.GetRequiredService<IAppCodeProvider>();
        _currentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        _engine = engine;
		_currentScriptUser = new ScriptUser(_currentUser);

    }

#pragma warning disable CA1822 // Mark members as static
	public void SetPath(String _/*path*/)
#pragma warning restore CA1822 // Mark members as static
	{
		//_currentPath = path;
	}

	public ScriptConfig config => _config;
    public ScriptUser currentUser => _currentScriptUser;

    public ExpandoObject loadModel(ExpandoObject prms)
	{
		String? source = prms.Get<String>("source");
		String command = prms.GetNotNull<String>("procedure");
		Boolean forCurrentUser = prms.Get<Boolean>("forCurrentUser");
		ExpandoObject? dmParams = prms.Get<ExpandoObject>("parameters") ?? [];
		if (forCurrentUser)
		{
			SetCurrentUserParams(dmParams);
            source = _currentUser.Identity.Segment;
        }
        var dm = _dbContext.LoadModel(source, command, dmParams);
		return dm.Root;
	}

	public ExpandoObject saveModel(ExpandoObject prms)
	{
		String? source = prms.Get<String>("source");
		String command = prms.GetNotNull<String>("procedure");
		ExpandoObject data = prms.GetNotNull<ExpandoObject>("data");

        Boolean forCurrentUser = prms.Get<Boolean>("forCurrentUser");
        ExpandoObject? dmParams = prms.Get<ExpandoObject>("parameters")
            ?? [];
		if (forCurrentUser)
		{
			SetCurrentUserParams(dmParams);
			source = _currentUser.Identity.Segment;
		}
        
		var dm = _dbContext.SaveModel(source, command, data, dmParams);
		return dm.Root;
	}

	public ExpandoObject? executeSql(ExpandoObject prms)
	{
		String? source = prms.Get<String>("source");
		String command = prms.GetNotNull<String>("procedure");
        Boolean forCurrentUser = prms.Get<Boolean>("forCurrentUser");
        ExpandoObject? dmParams = prms.Get<ExpandoObject>("parameters") ?? [];
		if (forCurrentUser)
		{
			SetCurrentUserParams(dmParams);
            source = _currentUser.Identity.Segment;
        }
        return _dbContext.ReadExpando(source, command, dmParams);
	}

	public FetchResponse fetch(String url)
	{
		return fetch(url, null);
	}

	public FetchResponse fetch(String url, ExpandoObject? prms)
	{
		return FetchCommand.Execute(_httpClientFactory, url, prms);
	}

	public FetchResponse invokeCommand(String cmd, String baseUrl, ExpandoObject parameters)
	{
		throw new NotImplementedException();
	}

#pragma warning disable CA1822 // Mark members as static
	public String generateApiKey()
#pragma warning restore CA1822 // Mark members as static
	{
		Int32 size = 48;
		Byte[] data = RandomNumberGenerator.GetBytes(size);
		String res = Convert.ToBase64String(data);
		res = res.Remove(res.Length - 2);
		return res;
	}

#pragma warning disable CA1822 // Mark members as static
	public String toBase64(String source, int codePage, bool safe)
#pragma warning restore CA1822 // Mark members as static
	{
		var enc = Encoding.GetEncoding(codePage, new EncoderReplacementFallback(String.Empty), DecoderFallback.ReplacementFallback);
		var bytes = enc.GetBytes(source);
		var res = Convert.ToBase64String(bytes);
		if (safe)
			res = res.Replace('+', '-').Replace('/', '_').TrimEnd('=');
		return res;
	}

	public JsValue require(String fileName, ExpandoObject prms, ExpandoObject args)
	{
		fileName = fileName.RemoveHeadSlash().AddExtension("js");

        var stream = _appCodeProvider.FileStreamRO(fileName) 
			?? throw new InvalidOperationException($"File not found '{fileName}'");
		var sr = new StreamReader(stream);
		var script = sr.ReadToEnd();

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

    private void SetCurrentUserParams(ExpandoObject dbPrms)
	{
		dbPrms.SetNotNull("UserId", _currentUser.Identity.Id);
        dbPrms.SetNotNull("TenantId", _currentUser.Identity.Tenant);
    }
}

#pragma warning restore IDE1006 // Naming Styles

