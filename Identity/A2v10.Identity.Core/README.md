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

If you want to use the custom data protection storage, add it
```csharp
services.AddDataProtectionSqlServer<T>("AppName");
```
# appsettings.json section

```json
"Identity": {
  "UserStore": {
    "DataSource": "ConnectionStringName",
    "MultiTenant": false,
    "ValidationInterval": "00:05:00",
    "AuthenticatorIssuer": 'IssuerName'
  },
  "CookiePrefix": "Cookie_Prefix",
  "Providers": "Local,..."
},
"DemoAccount": {
    "Login": "DEMO_LOGIN",
    "Password": "DEMO_PASSWORD"
}
```

All values are optional.

# Related Packages

* [A2v10.Identity.ApiKey](https://www.nuget.org/packages/A2v10.Identity.ApiKey)
* [A2v10.Identity.Jwt](https://www.nuget.org/packages/A2v10.Identity.Jwt)
* [A2v10.Identity.UI](https://www.nuget.org/packages/A2v10.Identity.UI)


# Feedback

A2v10.Identity.Core is released as open source under the MIT license. 
Bug reports and contributions are welcome at the GitHub repository.
