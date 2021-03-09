using A2v10.Data.Interfaces;
using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IExternalReportInfo
	{
		Stream Stream { get; }
		String Name { get; }
		String ReportPath { get; }
		IDataModel DataModel { get; }
		ExpandoObject Variables { get; }
	}

	public interface IExternalReport
	{
		Task<IInvokeResult> Export(IExternalReportInfo reportInfo, ExportReportFormat format);
	}
}
