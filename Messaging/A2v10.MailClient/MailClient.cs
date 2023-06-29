// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using MimeKit.Text;
using MimeKit;
using MailKit.Net.Smtp;

using A2v10.Infrastructure;

namespace A2v10.MailClient;

public class MailClient : IMailService
{
	private readonly MailSettings _mailSettings = new();
	public MailClient(IConfiguration configuration)
	{
		var sect = configuration.GetRequiredSection("mailSettings");
		sect.Bind(_mailSettings);
	}

	public Task SendAsync(IMailMessage message)
	{
		return _mailSettings.DeliveryMethod switch
		{
			MailDeliveryMethod.FileSystem => SendFileSystemAsync(message),
			MailDeliveryMethod.Smtp => SendSmtpAsync(message),
			_ => throw new NotImplementedException(),
		};
	}

	public Task SendAsync(String to, String subject, String body)
	{
		return SendAsync(MailMessage.Create(_mailSettings.From, to, subject, body));
	}

	private MimeMessage CreateMimeMessage(IMailMessage message)
	{
		var mm = new MimeMessage();
		mm.From.Add(new MailboxAddress(String.Empty, _mailSettings.From));
		foreach (var to in message.To)
			mm.To.Add(new MailboxAddress(to.DisplayName, to.Address));
		foreach (var cc in message.Cc)
			mm.Cc.Add(new MailboxAddress(cc.DisplayName, cc.Address));
		foreach (var bcc in message.Bcc)
			mm.Bcc.Add(new MailboxAddress(bcc.DisplayName, bcc.Address));
		mm.Subject = message.Subject;
		mm.Body = new TextPart(TextFormat.Html)
		{
			Text = message.Body
		};
		return mm;	
	}

	private async Task SendSmtpAsync(IMailMessage message)
	{
		var mm = CreateMimeMessage(message);

		var client = new SmtpClient();

		if (_mailSettings.SkipCertificateValidation)
			client.ServerCertificateValidationCallback = (sender, cert, ch, err) => true;
		await client.ConnectAsync(_mailSettings.Host, _mailSettings.Port, false /*always false - TLS!*/);
		await client.AuthenticateAsync(_mailSettings.UserName, _mailSettings.Password);
		await client.SendAsync(mm);
		await client.DisconnectAsync(true);
	}

	private async Task SendFileSystemAsync(IMailMessage message)
	{
		if (_mailSettings.PickupDirectoryLocation == null)
			throw new InvalidOperationException("PickupDirectoryLocation is null");
		var mm = CreateMimeMessage(message);
		var dir = _mailSettings.PickupDirectoryLocation.Replace("\\", "/");
		var path = Path.GetFullPath(Path.Combine(dir, $"{Guid.NewGuid()}.eml"));
		var file = new FileStream(path, FileMode.Create);
		await mm.WriteToAsync(new FormatOptions() { International = true }, file);
	}
}
