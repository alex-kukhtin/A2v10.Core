# A2v10.Web.Identity.UI

The user interface for authentication.

Part of the [A2v10.Platform](https://www.nuget.org/packages/A2v10.Plaform) package



# External Provider Authentication

## Create the External Application

* [Microsoft Account](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-8.0)
* [Google Account](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-8.0)

## Install External login package(s)

* [Microsoft.AspNetCore.Authentication.MicrosoftAccount](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.MicrosoftAccount/8.0.0?_src=template) for Microsoft Account
* [Microsoft.AspNetCore.Authentication.Google](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.Google/8.0.0?_src=template) for Google Account

## Configure platform

Store ClientId and ClientSecret in appsettings.json and enable external providers

```json
"Identity": {
	"Providers": "Local,Google,Microsoft",
	"Google": {
		"ClientId": "GOOGLE_CLIENT_ID",
		"ClientSecret": "GOOGLE_CLIENT_SECRET"
	},
	"Microsoft": {
		"ClientId": MICROSOFT_CLIENT_ID",
		"ClientSecret": "MICROSOFT_CLIENT_SECRET"
	}
}
```

Configure services

```csharp
public void ConfigureServices(IServiceCollection services)

	var builders = services.UsePlatform(Configuration);

    builders.AuthenticationBuilder.AddGoogle(opts =>
	{
		opts.ClientId = Configuration.GetValue<String>("Identity:Google:ClientId");
		opts.ClientSecret = Configuration.GetValue<String>("Identity:Google:ClientSecret");
		opts.Events.OnRemoteFailure = OpenIdErrorHandlers.OnRemoteFailure;
	})
	.AddMicrosoftAccount(opts =>
	{
		opts.ClientId = Configuration.GetValue<String>("Identity:Microsoft:ClientId");
		opts.ClientSecret = Configuration.GetValue<String>("Identity:Microsoft:ClientSecret");
		opts.Events.OnRemoteFailure = OpenIdErrorHandlers.OnRemoteFailure;
	});
}
```