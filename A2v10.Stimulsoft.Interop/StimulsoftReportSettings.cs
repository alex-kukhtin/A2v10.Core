using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Stimulsoft.Report.Export;

namespace A2v10.Stimulsoft.Interop
{
	public static class StimulsoftReportSettings
	{
		public static StiPdfExportSettings PdfExportSettings =>
			new StiPdfExportSettings
			{
				UseUnicode = true,
				EmbeddedFonts = true,
				ImageResolution = 300,
				ImageCompressionMethod = StiPdfImageCompressionMethod.Flate
			};

		public static StiExcel2007ExportSettings ExcelExportSettings =>
			new StiExcel2007ExportSettings
			{
				UseOnePageHeaderAndFooter = true
			};

		public static StiWord2007ExportSettings WordExportSettings =>
			new StiWord2007ExportSettings()
			{
			};
	}
}
