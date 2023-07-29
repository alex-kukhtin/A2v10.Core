// Copyright © 2023 Olekdsandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class NullUserBannerProvider : IUserBannerProvider
{

    public Task<String?> GetHtmlAsync()
    {
        return Task.FromResult<String?>(null);
    }
}
