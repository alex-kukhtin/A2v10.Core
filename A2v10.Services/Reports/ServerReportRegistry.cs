using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Services
{
	public static class ServerReportRegistry
	{
		public static IModelReportHandler GetReportHandler(ModelJsonReportType type, IServiceProvider serviceProvider)
		{
			return type switch
			{
				ModelJsonReportType.stimulsoft =>
					new ServerReportStimulsoft(
						serviceProvider.GetService<IExternalReport>(),
						serviceProvider.GetService<IAppCodeProvider>(),
						serviceProvider.GetService<IDbContext>(),
						serviceProvider.GetService<IUserStateManager>()
					),
				_ => 
				throw new NotImplementedException($"ReportHandler yet not implemented ({type})")
			};
		}
	}
}
