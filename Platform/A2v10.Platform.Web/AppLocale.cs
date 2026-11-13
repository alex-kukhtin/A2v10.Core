// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.Extensions.Configuration;

namespace A2v10.Platform.Web;

/* The application's own locale: the culture the server runs in and the dictionary a user without a
 * choice of their own is answered from. One function because two hosts read it - the server
 * (ConfigurePlatform) and the CLI, which has to look keys up exactly where a page does.
 */
public static class AppLocale
{
    public static String Of(IConfiguration? configuration)
    {
        var locale = configuration?.GetValue<String>("Globalization:Locale");
        return String.IsNullOrEmpty(locale) ? "uk-UA" : locale;
    }
}
