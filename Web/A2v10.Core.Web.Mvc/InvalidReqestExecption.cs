using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Core.Web.Mvc
{
	public sealed class InvalidReqestExecption : Exception
	{
		public InvalidReqestExecption(String message)
			:base(message)
		{

		}
	}
}
