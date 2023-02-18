﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

using Jint;

namespace A2v10.Services.Javascript;

public class JavaScriptEngine
{
    private readonly Engine _engine;
    private readonly ScriptEnvironment _environment;

    public JavaScriptEngine(IServiceProvider serviceProvider)
    {
        _engine = new Engine((opts) =>
        {
            opts.Strict(true);
        });
        _environment = new ScriptEnvironment(_engine, serviceProvider);
    }

    public void SetPath(String path)
    {
        _environment.SetPath(path);
    }

    public Object Execute(String script, ExpandoObject prms, ExpandoObject args)
    {

        var strPrms = JsonConvert.ToString(JsonConvert.SerializeObject(prms), '\'', StringEscapeHandling.Default);
        var strArgs = JsonConvert.ToString(JsonConvert.SerializeObject(args), '\'', StringEscapeHandling.Default);

        String code = $@"
return (function() {{
const __params__ = JSON.parse({strPrms});
const __args__ = JSON.parse({strArgs});
const module = {{exports:null }};

{script};

const __exp__ = module.exports;

return function(_this) {{
	return __exp__.call(_this, __params__, __args__);
}};

}})();";

        var func = _engine.Evaluate(code);
        var result = _engine.Invoke(func, _environment);

        return result.ToObject();
    }
}
