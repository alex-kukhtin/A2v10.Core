// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using A2v10.Scheduling.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Services;

public class ScheduledExecuteSqlCommand : IScheduledCommand
{
    private readonly ILogger<ScheduledExecuteSqlCommand> _logger;
    private readonly IDbContext _dbContext;
    public ScheduledExecuteSqlCommand(ILogger<ScheduledExecuteSqlCommand> logger, IDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }
    public async Task ExecuteAsync(String? Data)
    {
        _logger.LogInformation("ExecuteSqlCommand at {Time}, Data = {Data}", DateTime.Now, Data);
        if (Data == null || String.IsNullOrEmpty("Data"))
                throw new InvalidOperationException("ExecuteSqlCommand. Data is empty");
        var dat = JsonConvert.DeserializeObject<ExpandoObject>(Data) 
            ?? throw new InvalidOperationException("ExecuteSqlCommand. Invalid json");
        String proc = dat.Get<String>("Procedure") ??
                throw new InvalidOperationException("ExecuteSqlCommand. Procedure is null");
        await _dbContext.ExecuteExpandoAsync(dat.Get<String>("DataSource"),
            proc, dat.Get<ExpandoObject>("Parameters") ?? new ExpandoObject());
    }
}
