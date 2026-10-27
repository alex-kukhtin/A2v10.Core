// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Identity.UI;

// The phone of the account pages (register, invite), in std:mask's format: '#' a digit, the rest as typed.
// A constant until a host needs another country: then it becomes an option, and this is the one line to change.
internal static class PhoneMask
{
	public const String Value = "+38 (0##) ###-##-##";
}
