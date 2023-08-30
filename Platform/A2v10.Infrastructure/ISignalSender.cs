// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface ISignalSender
{
    Task SendAsync(ISignalResult message);
}
