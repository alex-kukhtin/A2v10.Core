// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace A2v10.Services;

public class BackgroundProcessHandler(IServiceScopeFactory _serviceScopeFactory, ILogger<BackgroundProcessHandler> _logger) 
    : IBackgroundProcessHandler
{
    public void Execute(Func<IServiceProvider, Task> action)
    {
        Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Background task started");
                using var scope = _serviceScopeFactory.CreateScope();
                await action(scope.ServiceProvider);
                _logger.LogInformation("Background task completed");
            }
            catch (Exception ex)
            {
                _logger.LogError("Background task failed. {ex}", ex.ToString());
            }
        });
    }
}
