
using InfinityModEngine.Enums;
using System.Collections.Generic;

namespace InfinityModEngine.Models
{
	public class ModLoadResult
	{
		public BaseModConfiguration modData;
		public readonly string modFileName;
		public readonly ModLoadStatus status;
		public readonly IEnumerable<string> loadErrors;

		public ModLoadResult(string modFileName, BaseModConfiguration modData, ModLoadStatus status, IEnumerable<string> loadErrors = null)
		{
			this.modFileName = modFileName;
			this.modData = modData;
			this.status = status;
			this.loadErrors = loadErrors ?? new List<string>();
		}
	}
}