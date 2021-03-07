
using System;

namespace A2v10.Infrastructure
{
	public interface IViewProvider
	{
		IViewEngine FindRenderer(String fileName);
	}
}
