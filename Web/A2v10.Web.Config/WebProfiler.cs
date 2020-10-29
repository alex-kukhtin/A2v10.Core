using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace A2v10.Web.Config
{

	public class NullRequest : IProfileRequest
	{
		public IDisposable Start(ProfileAction kind, String description)
		{
			return null;
		}
		public void Stop()
		{

		}
	}

	public class WebProfiler : IProfiler, IDataProfiler
	{
		readonly IProfileRequest _request = new NullRequest();

		#region IProfiler
		public bool Enabled { get; set; }

		public IProfileRequest CurrentRequest => _request;

		public IProfileRequest BeginRequest(String address, String session)
		{
			return _request;
		}

		public String GetJson()
		{
			return null;
		}

		#endregion

		#region IDataProfiler
		public IDisposable Start(String command)
		{
			return null;
		}
		#endregion
	}
}
