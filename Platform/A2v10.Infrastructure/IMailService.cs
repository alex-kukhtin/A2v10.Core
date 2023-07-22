// Copyright © 2023 Olekdsandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IMailMessageAddress
{
	String Address { get; }
	String? DisplayName { get; }
}

public interface IMailMessageAttachment
{
	Stream Stream { get; }
	String Name { get; }
	String Mime { get; }
}

public interface IMailMessage
{
	String Subject { get; set; }
	String Body { get; set; }

	IEnumerable<IMailMessageAddress> To { get; }
	IEnumerable<IMailMessageAddress> Cc { get; }
	IEnumerable<IMailMessageAddress> Bcc { get; }
	IEnumerable<IMailMessageAttachment> Attachments { get; }

	IMailMessageAddress? From { get; }

	void AddTo(String address, String? displayName = null);
	void AddCc(String address, String? displayName = null);
	void AddBcc(String address, String? displayName = null);
	void AddAttachment(Stream stream, String name, String mime);
}

public interface IMailService
{
	Task SendAsync(IMailMessage message);
	Task SendAsync(String to, String subject, String body);
}
