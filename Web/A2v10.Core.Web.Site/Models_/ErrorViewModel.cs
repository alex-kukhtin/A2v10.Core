using System;

namespace A2v10.Core.Web.Site.Models;

public class ErrorViewModel
{
	public String? RequestId { get; set; }

	public Boolean ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
