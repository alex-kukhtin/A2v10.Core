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
			new()
			{
				UseUnicode = true,
				EmbeddedFonts = true,
				ImageResolution = 300,
				ImageCompressionMethod = StiPdfImageCompressionMethod.Flate
			};

		public static StiExcel2007ExportSettings ExcelExportSettings =>
			new()
			{
				UseOnePageHeaderAndFooter = true
			};

		public static StiWord2007ExportSettings WordExportSettings =>
			new()
			{
			};

		public static StiOdtExportSettings OdtExportSettings =>
			new()
			{
			};

		public static StiOdsExportSettings OdsExportSettings =>
			new()
			{
			};
	}
}
