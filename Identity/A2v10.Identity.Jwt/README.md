# About
A2v10.Identity.Jwt is a set of Jwt bearer identity services 
for the A2v10 platform applications.


# How to use

```csharp
// T - type of user identifier

services.AddPlatformIdentityCore<T>()
.AddIdentityConfiguration<T>(configuration)
.AddPlatformAuthentication()
.AddPlatformJwtBearer(configuration);
```

# appsettings.json configuration

```json
"Authentication": {
    "JwtBearer": {
        "Issuer": "token issuer",
        "Audience": "token Audience",
        "ExpireMinutes": 10080,
        "SecurityKey": "<SecurityKeyValue> (min 16 chars)"
    }
},
"Identity": {
    "UserStore": {
        "DataSource": "Connection String Name",
        "Schema": "Database Schema",
        "MultiTenant": true
    }
}
```

# Related Packages

* [A2v10.Identity.Core](https://www.nuget.org/packages/A2v10.Identity.Core)
* [A2v10.Identity.ApiKey](https://www.nuget.org/packages/A2v10.Identity.ApiKey)


# Feedback

A2v10.Identity.Jwt is released as open source under the MIT license. 
Bug reports and contributions are welcome at the GitHub repository.