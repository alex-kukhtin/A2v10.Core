using A2v10.Infrastructure;
using A2v10.System.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Xaml
{
	public class AppXamlReaderService : XamlReaderService
	{
		private readonly IAppCodeProvider _codeProvider;
		private readonly XamlServicesOptions _options;

		public AppXamlReaderService(IAppCodeProvider codeProvider)
		{
			_codeProvider = codeProvider;
			_options = new XamlServicesOptions()
			{
				OnCreateReader = (rdr) =>
				{
					rdr.InjectService<IAppCodeProvider>(_codeProvider);
					rdr.InjectService<IXamlReaderService>(this);
				}
			};
		}

		public override XamlServicesOptions Options => _options;
	}
}
