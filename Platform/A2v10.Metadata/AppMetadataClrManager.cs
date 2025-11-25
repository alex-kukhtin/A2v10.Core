// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.App.Infrastructure;

namespace A2v10.Metadata;

internal class AppMetadataClrManager(IAppClrProvider _appClirProvider) : IAppClrManager
{
    public async Task OnAfterSaveModelAsync(IPlatformUrl platformUrl, ExpandoObject model)
    {
        var elem = _appClirProvider.CreateElement(platformUrl.LocalPath, model);
        if (elem == null)
            return;
        if (elem is IClrElementEventSource eventSource && eventSource.AfterSave != null)
            await eventSource.AfterSave();
    }

    public async Task<Boolean> OnBeforeSaveModelAsync(IPlatformUrl platformUrl, ExpandoObject model)
    {
        var elem = _appClirProvider.CreateElement(platformUrl.LocalPath, model);
        if (elem == null)
            return true;
        if (elem is IClrElementEventSource eventSource && eventSource.BeforeSave != null)
        {
            var cancelToken = new CancelToken();    
            await eventSource.BeforeSave(cancelToken);
            if (cancelToken.Cancel)
            {
                if (!String.IsNullOrWhiteSpace(cancelToken.Message))
                    throw new InvalidOperationException($"UI:{cancelToken.Message}");   
                return false;
            }
            elem.ToExpando();
            return true;
        }
        return true;
    }
}
