using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Services
{
	public sealed class DataServiceException : Exception
	{
		public DataServiceException(String msg)
			:base(msg)
		{
		}
	}
}
