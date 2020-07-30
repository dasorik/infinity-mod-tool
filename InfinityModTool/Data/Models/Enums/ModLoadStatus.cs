using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Enums
{
	public enum ModLoadStatus
	{
		Success,
		UnspecifiedFailure,
		NoConfig,
		ConfigInvalid,
		ExtensionInvalid,
		UnsupportedVersion,
		DuplicateID
	}
}
