// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using MimeKit.Text;
using MimeKit;
using MailKit.Net.Smtp;

using A2v10.Infrastructure;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace A2v10.MailClient;

public class MailClient : IMailService
{
	private readonly MailSettings _mailSettings = new();
	private readonly ILogger<MailClient> _logger;
	public MailClient(IConfiguration configuration, ILogger<MailClient> logger)
	{
		var sect = configuration.GetRequiredSection("mailSettings");
		sect.Bind(_mailSettings);
		_logger = logger;
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
		if (message.From != null)
            mm.From.Add(new MailboxAddress(message.From.DisplayName, message.From.Address));
        else
            mm.From.Add(new MailboxAddress(_mailSettings.FromName, _mailSettings.From));

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

    private static SecureSocketOptions FromOptions(MailSecureOptions m)
	{
		return m switch
		{
			MailSecureOptions.None => SecureSocketOptions.None,
            MailSecureOptions.Auto => SecureSocketOptions.Auto,
            MailSecureOptions.SslOnConnect => SecureSocketOptions.SslOnConnect,
            MailSecureOptions.StartTls => SecureSocketOptions.StartTls,
            MailSecureOptions.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
			_ => throw new NotImplementedException()
        };
	}

    private async Task SendSmtpAsync(IMailMessage message)
	{
		var mm = CreateMimeMessage(message);

		var wt = Stopwatch.StartNew();
		_logger.LogInformation("Sending mail using SMPT. From {from} to {to}", message.From, message.To);
		var client = new SmtpClient();

		if (_mailSettings.SkipCertificateValidation)
			client.ServerCertificateValidationCallback = (sender, cert, ch, err) => true;
		await client.ConnectAsync(_mailSettings.Host, _mailSettings.Port, FromOptions(_mailSettings.Secure));
		await client.AuthenticateAsync(_mailSettings.UserName, _mailSettings.Password);
		await client.SendAsync(mm);
		await client.DisconnectAsync(true);
        _logger.LogInformation("Mail sent. It took {ms} ms", wt.ElapsedMilliseconds);
    }

    private async Task SendFileSystemAsync(IMailMessage message)
	{
		if (_mailSettings.PickupDirectoryLocation == null)
			throw new InvalidOperationException("PickupDirectoryLocation is null");
        _logger.LogInformation("Sending mail using FileSystem ({pickupDirectoryLocation}). From {from} to {to}", _mailSettings.PickupDirectoryLocation, message.From, message.To);
        var mm = CreateMimeMessage(message);
		var dir = _mailSettings.PickupDirectoryLocation.Replace("\\", "/");
		var path = Path.GetFullPath(Path.Combine(dir, $"{Guid.NewGuid()}.eml"));
		using var file = new FileStream(path, FileMode.Create);
		await mm.WriteToAsync(new FormatOptions() { International = true }, file);
	}
}
