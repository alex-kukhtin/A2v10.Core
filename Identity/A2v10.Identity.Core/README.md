# About
A2v10.IdentityCore is a set of basic identity services 
for the A2v10 platform applications.


# How to use

```csharp
// T - type of user identifier

services.AddPlatformIdentityCore<T>()
.AddIdentityConfiguration<T>(configuration)
.AddPlatformAuthentication(cookiePrefix);
```

# Related Packages

* [A2v10.Identity.ApiKey](https://www.nuget.org/packages/A2v10.Identity.ApiKey)


# Feedback

A2v10.Identity.Core is released as open source under the MIT license. 
Bug reports and contributions are welcome at the GitHub repository.