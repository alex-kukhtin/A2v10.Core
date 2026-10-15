// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

// a fixed report over a ledger - nothing is picked but the period and the filters (LedgerReportBuilder)
internal abstract class LedgerReportPage(ReportMetadata report, ReportGrouping grouping) : ReportPage(report, grouping)
{
    protected override Boolean HasChoices => false;
}
