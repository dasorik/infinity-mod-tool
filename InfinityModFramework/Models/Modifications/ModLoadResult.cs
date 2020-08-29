
using InfinityModFramework.Enums;
using System.Collections.Generic;

namespace InfinityModFramework.Models
{
	public class ModLoadResult
	{
		public readonly string modFileName;
		public readonly string modID;
		public readonly ModLoadStatus status;
		public readonly IEnumerable<string> loadErrors;

		public ModLoadResult(string modFileName, string modID, ModLoadStatus status, IEnumerable<string> loadErrors = null)
		{
			this.modFileName = modFileName;
			this.modID = modID;
			this.status = status;
			this.loadErrors = loadErrors ?? new List<string>();
		}
	}
}