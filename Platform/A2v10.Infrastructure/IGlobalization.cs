// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Infrastructure;

public interface IGlobalization
{
    public String? DateLocale { get; }
    public String? NumberLocale { get; }
    public String? IsAvailable(String lang);
    // Globalization:AvailableLocales, as written (uk-UA): what a user may choose
    public IReadOnlyCollection<String> AvailableLocales { get; }
}
