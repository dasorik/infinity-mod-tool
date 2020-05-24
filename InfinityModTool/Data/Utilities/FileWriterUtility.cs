using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace InfinityModTool.Utilities
{
	public class FileWriterUtility
	{
		public static void WriteToFile(string filePath, string text, int memoryOffset, bool insert, out int writeOffset)
		{
			byte[] buffer = Encoding.ASCII.GetBytes(text);
			WriteToFile(filePath, buffer, memoryOffset, insert, out writeOffset);
		}

		public static void WriteToFile(string filePath, byte[] buffer, int memoryOffset, bool insert, out int writeOffset)
		{
			byte[] fileBytes = File.ReadAllBytes(filePath);
			byte[] tempBuffer = new byte[insert ? fileBytes.Length + buffer.Length : fileBytes.Length];

			if (insert)
			{
				// Copy the files with the new bytes inserted in the middle
				Array.Copy(fileBytes, 0, tempBuffer, 0, memoryOffset);
				Array.Copy(buffer, 0, tempBuffer, memoryOffset, buffer.Length);
				Array.Copy(fileBytes, memoryOffset, tempBuffer, memoryOffset + buffer.Length, fileBytes.Length - memoryOffset);
			}
			else
			{
				Array.Copy(fileBytes, 0, tempBuffer, 0, fileBytes.Length);
				Array.Copy(buffer, 0, tempBuffer, memoryOffset, buffer.Length);
			}

			writeOffset = buffer.Length;
			File.WriteAllBytes(filePath, tempBuffer);
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
	}
}
