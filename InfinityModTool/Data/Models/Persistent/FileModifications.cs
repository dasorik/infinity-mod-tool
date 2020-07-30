using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Models
{
	public enum FileModificationType
	{
		Moved,
		Deleted,
		Edited,
		Replaced,
		Added
	}

	public class FileModification
	{
		public string FilePath;
		public FileModificationType Type;
		public bool ReservedFiled;

		public FileModification()
		{

		}

		public FileModification(string filePath, FileModificationType type, bool reservedFile)
		{
			this.FilePath = filePath;
			this.Type = type;
			this.ReservedFiled = reservedFile;
		}
	}
}
