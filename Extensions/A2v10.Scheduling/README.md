# About
A2v10.Scheduling is a basic job scheduling system
for the A2v10 platform applications.


# How to use

```csharp
services.UseScheduling(Configuration, factory =>
{
    // job handlers
    factory.RegisterJobHandler<T>("HandlerName");
    // or (the name will be equal typeof(T).FullName)
    factory.RegisterJobHandler<T>(); 
    // commands
    factory.RegisterCommand<T>("CommandName");
});
```

## appsettings.json section

```json
"Scheduler": {
  "Jobs": [
    {
       "Id": "JobId",
       "Handler": "HandlerName",
       "Cron": "0 * * ? * *",
       "DataSource": "ConnectionStringName",
       "Procedure": "dbo.[Sql_procedure]",
       "Parameters": {
         ...
       }
    },
  ]
}
```

"Id", "Handler", and "Cron" are required.
Rest parameters depend on the handler type.


# Hanlder types in this package

* **A2v10.Scheduling.ExecuteSqlJobHandler** - executes a stored procedure
* **A2v10.Scheduling.ProcessCommandsJobHandler** - processes the command queue



# See also

* [Cron Trigger Tutorial](http://www.quartz-scheduler.org/documentation/quartz-2.3.0/tutorials/crontrigger.html)


# Related Packages

* [A2v10.Scheduling.Infrastructure](https://www.nuget.org/packages/A2v10.Scheduling.Infrastructure)
* [A2v10.Data.Interfaces](https://www.nuget.org/packages/A2v10.Data.Interfaces)

* [Quartz.AspNetCore](https://www.nuget.org/packages/Quartz.AspNetCore)

# If your site is hosted using IIS 

Make sure the site is always running.

* Application Pool | Advanced Settings => Start Mode = AlwaysRunning
* Site | Advanced Settings => Preload Enabled = True

# Feedback

A2v10.Scheduling is released as open source under the Apache-2.0 license. 
Bug reports and contributions are welcome at the GitHub repository.
