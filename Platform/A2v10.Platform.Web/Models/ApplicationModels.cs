using System;

namespace A2v10.Platform.Web.Models;

public class SwitchToCompanySaveModel
{
    public Int64 UserId { get; set; }
    public Int32? TenantId { get; set; }
    public Int64 CompanyId { get; set; }
}

