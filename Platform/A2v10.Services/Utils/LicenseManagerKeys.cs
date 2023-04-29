using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Services;

internal partial class LicenseManager
{
	private static readonly IReadOnlyDictionary<String, String> _publicKeys = new Dictionary<String, String>();
}
