// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services;

internal class ApiLocalizer : ILocalizer
{
    public String? this[String? index] => throw new NotImplementedException();

    public IDictionary<String, String> Dictionary => throw new NotImplementedException();

    public String? Localize(String? locale, String? content, Boolean replaceNewLine = true)
    {
        return content;
    }

    public string? Localize(string? content)
    {
        throw new NotImplementedException();
    }
}
