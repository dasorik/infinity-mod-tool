using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModEngine.Models
{
	public enum FileModificationType
	{
		Moved,
		Deleted,
		Edited,
		Replaced,
		Added,
		QuickBMSExtracted
	}

	public class FileModification
	{
		public string FilePath;
		public FileModificationType Type;
		public bool ReservedFiled;
		public string ModID;

		public FileModification()
		{

		}

		protected FileModification(string filePath, FileModificationType type, bool reservedFile, string modID)
		{
			this.FilePath = filePath;
			this.Type = type;
			this.ReservedFiled = reservedFile;
			this.ModID = modID;
		}
	}

	public class DeleteFileModification : FileModification
	{
		public DeleteFileModification(string filePath, bool reservedFile, string modID)
			: base(filePath, FileModificationType.Deleted, reservedFile, modID)
		{

		}
	}

	public class EditFileModification : FileModification
	{
		public EditFileModification(string filePath, bool reservedFile, string modID)
			: base(filePath, FileModificationType.Edited, reservedFile, modID)
		{

		}
	}

	public class AddFileModification : FileModification
	{
		public AddFileModification(string filePath, bool reservedFile, string modID)
			: base(filePath, FileModificationType.Added, reservedFile, modID)
		{

		}
	}

	public class MoveFileModification : FileModification
	{
		public string DestinationPath;

		public MoveFileModification(string filePath, string destinationPath, bool reservedFile, string modID)
			: base(filePath, FileModificationType.Moved, reservedFile, modID)
		{
			this.DestinationPath = destinationPath;
		}
	}

	public class ReplaceFileModification : FileModification
	{
		public ReplaceFileModification(string filePath, bool reservedFile, string modID)
			: base(filePath, FileModificationType.Replaced, reservedFile, modID)
		{

		}
	}
	
	public class QuickBMSExtractModication : FileModification
	{
		public bool AutoUnpacked;

		public QuickBMSExtractModication(string filePath, bool autoUnpacked, bool reservedFile, string modID)
			: base(filePath, FileModificationType.QuickBMSExtracted, reservedFile, modID)
		{
			this.AutoUnpacked = autoUnpacked;
		}
	}
}
