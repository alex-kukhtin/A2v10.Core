using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public enum UrlKind
	{
		Undefined,
		Page,
		Dialog,
		Poupup
	}

	public interface IPlatformUrl
	{
		String LocalPath { get; }
		UrlKind Kind { get; }
		String Action { get; }
	}
}
