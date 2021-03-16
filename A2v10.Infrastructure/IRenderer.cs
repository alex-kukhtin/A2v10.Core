
using System.IO;

namespace A2v10.Infrastructure
{
	public interface IRenderer
	{
		void Render(IRenderInfo info, TextWriter writer);
	}
}
