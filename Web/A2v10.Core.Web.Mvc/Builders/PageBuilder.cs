using A2v10.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace A2v10.Core.Web.Mvc.Builders
{
	public class PageBuilder
	{
		private readonly IDataScripter _scripter;

		public PageBuilder()
		{
			_scripter = new VueDataScripter();
		}
	}
}
