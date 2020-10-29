
using System;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Web.Config
{
	public class WebLocalizer : ILocalizer, IDataLocalizer
	{
		#region ILocalizer
		public String Localize(String locale, String content, bool replaceNewLine = true)
		{
			return content;
		}
		#endregion

		#region IDataLocalizer
		public String Localize(String content)
		{
			return content;
		}
		#endregion
	}
}
