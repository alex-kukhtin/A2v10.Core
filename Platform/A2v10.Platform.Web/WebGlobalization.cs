// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;
using System.Diagnostics.CodeAnalysis;

namespace A2v10.Platform.Web;


public class WebGlobalization : IGlobalization
{
    private readonly Dictionary<String, String> _availableLanguages = new(StringComparer.OrdinalIgnoreCase);
    public String? DateLocale { get; private set; }
    public String? NumberLocale { get; private set; }

    public WebGlobalization(IConfiguration _config)
    {
        DateLocale = _config.GetValue<String>("Globalization:DateLocale");
        NumberLocale = _config.GetValue<String>("Globalization:NumberLocale");

        var langs = _config.GetValue<String>("Globalization:AvailableLocales");
        if (langs == null)
            return;
        _availableLanguages = langs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToDictionary(k => k[..2], v => v );
    }

    public String? IsAvailable(String lang)
    {
        if (_availableLanguages.TryGetValue(lang, out String? realValue))
            return realValue;
        return null;
    }
}
