// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class ExternalReportInfo : IReportInfo
	{
		public Stream Stream { get; init; }
		public String Name { get; init; }
		public String Path { get; init; }
		public String Report { get; init; }
		public IDataModel DataModel { get; init; }
		public ExpandoObject Variables { get; init; }
	}
}
