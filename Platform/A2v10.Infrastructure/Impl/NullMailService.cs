using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class NullMailService : IMailService
{
	public Task SendAsync(IMailMessage message)
	{
		throw new NotImplementedException();
	}

	public Task SendAsync(string to, string subject, string body)
	{
		throw new NotImplementedException();
	}
}
