using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Services
{
	public class ExternalReportInfo : IExternalReportInfo
	{
		public Stream Stream { get; init; }
		public String Name { get; init; }
		public String ReportPath { get; init; }
		public IDataModel DataModel { get; init; }
		public ExpandoObject Variables { get; init; }
	}
}
