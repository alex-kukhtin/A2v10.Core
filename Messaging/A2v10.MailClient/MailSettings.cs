// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.MailClient;

public enum MailDeliveryMethod
{
	FileSystem,
	Smtp,
}

public record MailSettings
{
	public String Host { get; init; } = String.Empty;
	public Int32 Port { get; init; }
	public String From { get; init; } = String.Empty;
	public String UserName { get; init; } = String.Empty;
	public String Password { get; init; } = String.Empty;
	public Boolean EnableSsl { get; init; }
	public Boolean SkipCertificateValidation { get; set; }
	public MailDeliveryMethod DeliveryMethod { get; init; }
	public String? PickupDirectoryLocation { get; init; }
}
