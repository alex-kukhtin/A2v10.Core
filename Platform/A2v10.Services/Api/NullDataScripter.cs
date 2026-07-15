
// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;
using A2v10.Data.Interfaces;

namespace A2v10.Services;

internal class NullDataScripter : IDataScripter
{
    public String CreateScript(IDataHelper helper, IReadOnlyDictionary<string, object?>? sys, IDictionary<string, IDataMetadata> meta) => String.Empty;

    public Task<ScriptInfo> GetModelScript(ModelScriptInfo msi) => Task.FromResult<ScriptInfo>(new ScriptInfo(String.Empty, String.Empty));

    public ScriptInfo GetServerScript(ModelScriptInfo msi) => new ScriptInfo(String.Empty, String.Empty);
}
