// Copyright © 2021 Alex Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.ReportEngine.Stimulsoft.Controllers
{

	[Route("stimulsoft/[action]")]
	public class StimulsoftController : Controller
	{
		public IActionResult Show()
		{
			return new EmptyResult();
		}
	}
}
