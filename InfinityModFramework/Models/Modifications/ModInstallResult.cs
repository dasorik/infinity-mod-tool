using InfinityModFramework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModFramework.Models
{
	public class ModInstallResult
	{
		public readonly InstallationStatus status;
		public readonly IEnumerable<ModCollision> conflicts;

		public ModInstallResult(InstallationStatus status)
			: this(status, new ModCollision[0]) { }

		public ModInstallResult(InstallationStatus status, IEnumerable<ModCollision> conflicts)
		{
			this.status = status;
			this.conflicts = conflicts;
		}
	}
}
