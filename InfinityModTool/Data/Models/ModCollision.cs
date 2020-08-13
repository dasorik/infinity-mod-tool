using InfinityModTool.Data.Modifications;
using InfinityModTool.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Models
{
	public class ModCollision
	{
		public readonly string modID;
		public readonly ModCollisionSeverity severity;
		public readonly string description;

		public ModCollision(string modID, ModCollisionSeverity severity, string description)
		{
			this.modID = modID;
			this.severity = severity;
			this.description = description;
		}
	}
}
