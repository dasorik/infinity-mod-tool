using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InfinityModTool.Utilities
{
	public struct FileWrite
	{
		public readonly long localStartOffset;
		public readonly long localEndOffset;
		public readonly long bytesWritten;
		public readonly long bytesAdded;

		public FileWrite(long localStartOffset, long localEndOffset, long bytesWritten, long bytesAdded)
		{
			this.localStartOffset = localStartOffset;
			this.localEndOffset = localEndOffset;
			this.bytesWritten = bytesWritten;
			this.bytesAdded = bytesAdded;
		}
	}

	public interface IWriteContent
	{
		long StartOffset { get; }
		long? EndOffset { get; }
		string DataFilePath { get; }
		string Text { get; }
		bool Replace { get; }
	}

	public class FileWriterUtility
	{
		private Dictionary<string, List<FileWrite>> writeCache = new Dictionary<string, List<FileWrite>>();

		public FileWrite WriteToFile(string filePath, string text, long memoryOffset, bool insert, bool ignoreWriteCache)
		{
			byte[] buffer = Encoding.ASCII.GetBytes(text);
			return WriteToFile(filePath, buffer, memoryOffset, insert, ignoreWriteCache);
		}

		public bool CanWrite(string targetFile, IWriteContent content)
		{
			// Check if we don't have any writes to the file yet
			if (!writeCache.TryGetValue(targetFile, out List<FileWrite> fileWrites))
				return true;

			foreach (var write in fileWrites)
			{
				if (!CanWrite(content, write))
					return false;
			}

			return true;
		}

		public bool CanWrite(IWriteContent content, FileWrite write)
		{
			long contentLength = content.EndOffset.HasValue ? content.EndOffset.Value - content.StartOffset : 0;
			long contentEndOffset = content.StartOffset + contentLength;

			bool startsInRange = content.StartOffset > write.localStartOffset && content.StartOffset < write.localEndOffset;
			bool endsInRange = contentEndOffset > write.localStartOffset && contentEndOffset < write.localEndOffset;

			// We're attempting to write to a section of the file that has been modified by another
			if (startsInRange || (content.Replace && endsInRange))
				return false;

			return true;
		}

		public FileWrite WriteToFile(string filePath, byte[] buffer, long memoryOffset, bool insert, bool ignoreWriteCache)
		{
			var actualOffset = ignoreWriteCache ? memoryOffset : GetOffsetForFile(memoryOffset, filePath);

			byte[] fileBytes = File.ReadAllBytes(filePath);
			byte[] tempBuffer = new byte[insert ? fileBytes.Length + buffer.Length : fileBytes.Length];

			if (insert)
			{
				// Copy the files with the new bytes inserted in the middle
				Array.Copy(fileBytes, 0, tempBuffer, 0, actualOffset);
				Array.Copy(buffer, 0, tempBuffer, actualOffset, buffer.Length);
				Array.Copy(fileBytes, actualOffset, tempBuffer, actualOffset + buffer.LongLength, fileBytes.LongLength - actualOffset);
			}
			else
			{
				Array.Copy(fileBytes, 0, tempBuffer, 0, fileBytes.Length);
				Array.Copy(buffer, 0, tempBuffer, actualOffset, buffer.Length);
			}

			var writeInfo = InsertToWriteCache(filePath, memoryOffset, memoryOffset + (insert ? 0 : buffer.Length), buffer.Length, insert ? buffer.Length : 0);
			File.WriteAllBytes(filePath, tempBuffer);

			return writeInfo;
		}

		public FileWrite WriteToFileRange(string filePath, string text, long startOffset, long endOffset, bool ignoreWriteCache)
		{
			var actualStartOffset = ignoreWriteCache ? startOffset : GetOffsetForFile(startOffset, filePath);
			var actualEndOffset = ignoreWriteCache ? endOffset : GetOffsetForFile(endOffset, filePath);
			var replaceRange = actualEndOffset - actualStartOffset;

			byte[] fileBytes = File.ReadAllBytes(filePath);
			byte[] textBuffer = Encoding.ASCII.GetBytes(text);
			byte[] tempBuffer = new byte[actualStartOffset + text.Length + (fileBytes.LongLength - actualEndOffset)];

			// Copy the files with the new bytes inserted in the middle
			Array.Copy(fileBytes, 0, tempBuffer, 0, actualStartOffset);
			Array.Copy(textBuffer, 0, tempBuffer, actualStartOffset, textBuffer.Length);
			Array.Copy(fileBytes, actualEndOffset, tempBuffer, actualStartOffset + textBuffer.LongLength, fileBytes.LongLength - actualEndOffset);

			var writeInfo = InsertToWriteCache(filePath, startOffset, endOffset, text.Length, text.Length - replaceRange);
			File.WriteAllBytes(filePath, tempBuffer);

			return writeInfo;
		}

		// TODO: We need a nice way to deal with removal of text
		private FileWrite InsertToWriteCache(string filePath, long localStartOffset, long localEndOffset, long bytesWritten, long bytesAdded)
		{
			var writeInfo = new FileWrite(localStartOffset, localEndOffset, bytesWritten, bytesAdded);

			if (!writeCache.ContainsKey(filePath))
				writeCache.Add(filePath, new List<FileWrite>());

			writeCache[filePath].Add(writeInfo);
			return writeInfo;
		}

		private long GetOffsetForFile(long memoryOffset, string file)
		{
			if (!writeCache.ContainsKey(file))
				return memoryOffset;

			var orderedWrites = writeCache[file].OrderBy(w => w.localStartOffset);
			long newMemoryOffset = 0;

			foreach (var write in orderedWrites)
			{
				if (write.localStartOffset <= memoryOffset)
					newMemoryOffset += write.bytesAdded;
				else
					break;
			}

			return newMemoryOffset + memoryOffset;
		}

		public static string GetUniqueFilePath(string path)
		{
			if (!File.Exists(path))
				return path;

			var fileInfo = new FileInfo(path);
			var fileName = Path.GetFileNameWithoutExtension(path);
			var directory = fileInfo.DirectoryName;
			var files = new HashSet<string>(Directory.GetFiles(directory));

			var originalPath = path;
			int index = 1;

			do
			{
				path = Path.Combine(directory, $"{fileName}_{index}{fileInfo.Extension}");
			}
			while (files.Contains(path));

			return path;
		}
	}
}
