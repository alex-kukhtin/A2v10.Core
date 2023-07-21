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


# Feedback

A2v10.Scheduling is released as open source under the MIT license. 
Bug reports and contributions are welcome at the GitHub repository.