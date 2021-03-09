using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public enum ExportReportFormat
	{
		Pdf,
		Excel,
		Word
	};

	public interface IReportService
	{
		Task<IInvokeResult> ExportAsync(String url, ExportReportFormat format, Action<ExpandoObject> setParams);
	}
}
