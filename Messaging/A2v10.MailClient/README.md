# About
A2v10.MailClient is a simple wrapper for using the MimeKit package 
in A2v10 applications.


# Related Packages

* [MimeKit](https://www.nuget.org/packages/MimeKit)
* [A2v10.Infrastructure](https://www.nuget.org/packages/A2v10.Infrastructure)


# How to use

```csharp

	//!!!Before Use Platform. It has a default implementation. 
	services.UseMailClient();
}
```

# appsettings.json section

```json
"MailSettings": {
	"Host": "host name",
	"Port": <port>, /* 25, 587 (SSL), 465 (SMTP over SSL) */
	"From": "from_address",
	"FromName": "from_display_name"
	"UserName": "user_name",
	"Password": "user_password",
	"Secure": "None" | "Auto" | "SslOnConnect" | "StartTls" | "StartTlsWhenAvailable",
	"SkipCertificateValidation": true,
	"DeliveryMethod": "FileSystem" | "Smtp",
	"PickupDirectoryLocation": "c:/dir_for_messages_for_file_system"
}
```

