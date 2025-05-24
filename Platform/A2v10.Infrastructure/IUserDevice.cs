// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public interface IUserDevice
{
    public Boolean IsMobile { get; }
    public Boolean IsDesktop { get; }
    public Boolean IsTablet { get; }
}
