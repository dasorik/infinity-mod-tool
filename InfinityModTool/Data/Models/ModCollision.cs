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
		public readonly GameModification mod;
		public readonly ModCollisionSeverity severity;
		public readonly string description;

		public ModCollision(GameModification mod, ModCollisionSeverity severity, string description)
		{
			this.mod = mod;
			this.severity = severity;
			this.description = description;
		}
	}
}
