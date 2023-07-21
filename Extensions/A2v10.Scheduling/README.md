# About
A2v10.Scheduling is a basic job scheduling system
for the A2v10 platform applications.


# How to use

```csharp
services.UseScheduling(Configuration, factory =>
{
	// job handlers
	factory.RegisterJobHandler<T>("HandlerName");
    // commands
    factory.RegisterCommand<T>("CommandName");
});
```

# Related Packages

* [A2v10.Scheduling.Infrastructure](https://www.nuget.org/packages/A2v10.Scheduling.Infrastructure)

# Hosting Website using IIS 

Application Pool / Advanced Settings / Start Mode = AlwaysRunning
Site | Advanced Settings | Preload Enabled = True

# Feedback

A2v10.Scheduling is released as open source under the MIT license. 
Bug reports and contributions are welcome at the GitHub repository.