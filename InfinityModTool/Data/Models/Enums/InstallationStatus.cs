using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Enums
{
	public enum InstallationStatus
	{
		Success,
		ResolvableConflict,
		UnresolvableConflict,
		RolledBackError,
		FatalError
	}
}
