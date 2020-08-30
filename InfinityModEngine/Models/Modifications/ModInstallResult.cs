using InfinityModEngine.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModEngine.Models
{
	public class ModInstallResult
	{
		public readonly InstallationStatus status;
		public readonly IEnumerable<ModCollision> conflicts;
		public readonly IEnumerable<FileModification> fileModifications;

		public ModInstallResult(InstallationStatus status, IEnumerable<ModCollision> conflicts, IEnumerable<FileModification> fileModifications)
		{
			this.status = status;
			this.conflicts = conflicts;
			this.fileModifications = fileModifications;
		}
	}
}
