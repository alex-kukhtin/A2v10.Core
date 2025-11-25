// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IAppClrManager
{
    /// <summary>
    /// returns false if save is not allowed
    /// <summary>
    Task<Boolean> OnBeforeSaveModelAsync(IPlatformUrl platformUrl, ExpandoObject model);
    Task OnAfterSaveModelAsync(IPlatformUrl platformUrl, ExpandoObject model);
}
