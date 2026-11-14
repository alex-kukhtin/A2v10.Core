// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Identity.UI;

// the shell's own switch of the locale and the theme; the write is IAppUserProfile's
public class SetPreferencesHandler(IServiceProvider serviceProvider) : IClrInvokeTarget
{
    private readonly IAppUserProfile _profile = serviceProvider.GetRequiredService<IAppUserProfile>();

    public async Task<object> InvokeAsync(ExpandoObject args)
    {
        var locale = args.Get<String>("Locale")
			?? throw new InvalidOperationException("Locale is null");
        var theme = args.Get<String>("Theme")
            ?? throw new InvalidOperationException("Theme is null");

        await _profile.SetPersonalAsync(null, locale, theme);

        return new ExpandoObject();
    }
}
