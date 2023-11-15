// Copyright © 2023 Olekdsandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;

namespace A2v10.Infrastructure;

public record MessageAddress : IMailMessageAddress
{
	public MessageAddress(String address, String? displayName = null)
	{
		Address = address;
		DisplayName = displayName;	
	}
	public String Address { get; }
	public String? DisplayName { get; }

    public override string ToString()
    {
		var t = Address;
		if (DisplayName != null)
			t += $" ({DisplayName})";
		return t;
    }
}

public record MailMessage : IMailMessage
{
	private readonly List<MessageAddress> _to = [];
	private readonly List<MessageAddress> _cc = [];
	private readonly List<MessageAddress> _bcc = [];
	private readonly MessageAddress? _from;
	public MailMessage(String? from, String subject, String body)
	{
		Subject = subject;
		Body = body;
		if (!String.IsNullOrEmpty(from))
			_from = new MessageAddress(from);
	}
	public String Subject { get; set; }
	public String Body { get; set; }

	public IEnumerable<IMailMessageAttachment> Attachments => throw new NotImplementedException();

	public IMailMessageAddress? From => _from;

	public IEnumerable<IMailMessageAddress> To => _to;
	public IEnumerable<IMailMessageAddress> Cc => _cc;
	public IEnumerable<IMailMessageAddress> Bcc => _bcc;

	public void AddAttachment(Stream stream, String name, String mime)
	{
		throw new NotImplementedException();
	}
	public void AddTo(String address, String? displayName = null)
	{
		_to.Add(new MessageAddress(address, displayName));
	}

	public void AddCc(String address, String? displayName = null)
	{
		_cc.Add(new MessageAddress(address, displayName));
	}
	public void AddBcc(String address, String? displayName = null)
	{
		_bcc.Add(new MessageAddress(address, displayName));
	}

	public static MailMessage Create(String from, String to, String subject, String body)
	{
		var mm = new MailMessage(from, subject, body);
		mm.AddTo(to);
		return mm;
	}
    public static MailMessage Create(String to, String subject, String body)
    {
        var mm = new MailMessage(null, subject, body);
        mm.AddTo(to);
        return mm;
    }
}
