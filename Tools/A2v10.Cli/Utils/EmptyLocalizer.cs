
using System.Collections.Generic;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class EmptyLocalizer : ILocalizer
{
    public string? this[string? index] => index;

    public IDictionary<string, string> Dictionary => new Dictionary<string, string>();

    public string? Localize(string? locale, string? content, bool replaceNewLine = true)
    {
        return content;
    }

    public string? Localize(string? content)
    {
        return content;
    }
}
