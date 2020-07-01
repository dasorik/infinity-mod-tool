using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace InfinityModTool.Utilities
{
	public struct FileWrite
	{
		public readonly long offset;
		public readonly long bytesWritten;
		public readonly long bytesAdded;

		public FileWrite(long offset, long bytesWritten, long bytesAdded)
		{
			this.offset = offset;
			this.bytesWritten = bytesWritten;
			this.bytesAdded = bytesAdded;
		}
	}

	public class FileWriterUtility
	{
		public static FileWrite WriteToFile(string filePath, string text, long memoryOffset, bool insert, Dictionary<string, List<FileWrite>> writeCache, bool ignoreWriteCache)
		{
			byte[] buffer = Encoding.ASCII.GetBytes(text);
			return WriteToFile(filePath, buffer, memoryOffset, insert, writeCache, ignoreWriteCache);
		}

		public static FileWrite WriteToFile(string filePath, byte[] buffer, long memoryOffset, bool insert, Dictionary<string, List<FileWrite>> writeCache, bool ignoreWriteCache)
		{
			var actualOffset = ignoreWriteCache ? memoryOffset : GetOffsetForFile(memoryOffset, filePath, writeCache);

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

			var writeInfo = InsertToWriteCache(filePath, memoryOffset, buffer.Length, insert ? buffer.Length : 0, writeCache);
			File.WriteAllBytes(filePath, tempBuffer);

			return writeInfo;
		}

		public static FileWrite WriteToFileRange(string filePath, string text, long startOffset, long endOffset, Dictionary<string, List<FileWrite>> writeCache, bool ignoreWriteCache)
		{
			var actualStartOffset = ignoreWriteCache ? startOffset : GetOffsetForFile(startOffset, filePath, writeCache);
			var actualEndOffset = ignoreWriteCache ? endOffset : GetOffsetForFile(endOffset, filePath, writeCache);
			var replaceRange = actualEndOffset - actualStartOffset;

			byte[] fileBytes = File.ReadAllBytes(filePath);
			byte[] textBuffer = Encoding.ASCII.GetBytes(text);
			byte[] tempBuffer = new byte[actualStartOffset + text.Length + (fileBytes.LongLength - actualEndOffset)];

			// Copy the files with the new bytes inserted in the middle
			Array.Copy(fileBytes, 0, tempBuffer, 0, actualStartOffset);
			Array.Copy(textBuffer, 0, tempBuffer, actualStartOffset, textBuffer.Length);
			Array.Copy(fileBytes, actualEndOffset, tempBuffer, actualStartOffset + textBuffer.LongLength, fileBytes.LongLength - actualEndOffset);

			var writeInfo = InsertToWriteCache(filePath, startOffset, text.Length, text.Length - replaceRange, writeCache);
			File.WriteAllBytes(filePath, tempBuffer);

			return writeInfo;
		}

		// TODO: We need a nice way to deal with removal of text
		private static FileWrite InsertToWriteCache(string filePath, long offset, long bytesWritten, long bytesAdded, Dictionary<string, List<FileWrite>> writeCache)
		{
			var writeInfo = new FileWrite(offset, bytesWritten, bytesAdded);

			if (!writeCache.ContainsKey(filePath))
				writeCache.Add(filePath, new List<FileWrite>());

			writeCache[filePath].Add(writeInfo);
			return writeInfo;
		}

		public static string CreateTempFolder()
		{
			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var tempDirectory = Path.Combine(executionPath, "Temp");

			if (Directory.Exists(tempDirectory))
				Directory.Delete(tempDirectory, true);

			Directory.CreateDirectory(tempDirectory);

			return tempDirectory;
		}

		private static long GetOffsetForFile(long memoryOffset, string file, Dictionary<string, List<FileWrite>> writeCache)
		{
			if (!writeCache.ContainsKey(file))
				return memoryOffset;

			var orderedWrites = writeCache[file].OrderBy(w => w.offset);
			long newMemoryOffset = 0;

			foreach (var write in orderedWrites)
			{
				if (write.offset <= memoryOffset)
					newMemoryOffset += write.bytesAdded;
				else
					break;
			}

			return newMemoryOffset + memoryOffset;
		}
	}
}
