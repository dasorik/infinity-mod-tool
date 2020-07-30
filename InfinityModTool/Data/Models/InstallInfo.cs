using InfinityModTool.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Models
{
	public class InstallInfo
	{
		public readonly InstallationStatus status;
		public readonly ModCollision[] conflicts;

		public InstallInfo(InstallationStatus status)
			: this(status, new ModCollision[0]) { }

		public InstallInfo(InstallationStatus status, ModCollision[] conflicts)
		{
			this.status = status;
			this.conflicts = conflicts;
		}
	}
}
