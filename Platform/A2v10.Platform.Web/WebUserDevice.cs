// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;

using A2v10.Platform.Web.DeviceDetector;

namespace A2v10.Platform.Web;

public class WebUserDevice(IHttpContextAccessor _httpContextAccessor) : IUserDevice
{

    private Device _currentDevice = UserDeviceDetector.DeviceFromUserAgent(_httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"]);

    public Boolean IsMobile => _currentDevice.HasFlag(Device.Mobile);

    public Boolean IsDesktop => _currentDevice.HasFlag(Device.Desktop);

    public Boolean IsTablet => _currentDevice.HasFlag(Device.Tablet);
}
