// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using Stimulsoft.Report.Export;

namespace A2v10.ReportEngine.Stimulsoft
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
