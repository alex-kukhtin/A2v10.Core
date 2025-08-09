// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public interface IGlobalization
{
    public String? DateLocale { get; }
    public String? NumberLocale { get; }
    public String? IsAvailable(String lang);
}
