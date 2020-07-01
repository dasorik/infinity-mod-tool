using NUnit.Framework;
using InfinityModTool;
using InfinityModTool.Utilities;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

namespace InfinityModTool.Test
{
	public class FileWriterTests
	{
		string tempFile;
		Dictionary<string, List<FileWrite>> writeCache;

		[SetUp]
		public void Setup()
		{
			writeCache = new Dictionary<string, List<FileWrite>>();

			var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			tempFile = Path.Combine(executionPath, "TEST_0526c915-4036-480b-90c5-3a8ef2088de6.txt");

			if (File.Exists(tempFile))
				File.Delete(tempFile);

			File.WriteAllText(tempFile, "abcdefghijklmnopqrstuvqxyz1234567890");
		}

		[TearDown]
		public void TearDown()
		{
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}

		[Test]
		public void CheckWriteData_SingleWrite()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test", 5, true, writeCache, false);

			Assert.AreEqual(4, write.bytesWritten);
			Assert.AreEqual(5, write.offset);
		}

		[Test]
		public void CheckWriteData_MultipleWrites_After()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test1", 7, true, writeCache, false);
			var write2 = FileWriterUtility.WriteToFile(tempFile, "Test2", 10, true, writeCache, false);

			Assert.AreEqual(5, write.bytesWritten);
			Assert.AreEqual(7, write.offset);

			Assert.AreEqual(5, write2.bytesWritten);
			Assert.AreEqual(10, write2.offset);
		}

		[Test]
		public void CheckWriteData_MultipleWrites_Before()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test1", 10, true, writeCache, false);
			var write2 = FileWriterUtility.WriteToFile(tempFile, "Test2", 7, true, writeCache, false);

			Assert.AreEqual(5, write.bytesWritten);
			Assert.AreEqual(10, write.offset);

			Assert.AreEqual(5, write2.bytesWritten);
			Assert.AreEqual(7, write2.offset);
		}

		[Test]
		public void ValidateText_SingleWrite()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test", 8, true, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdefghTestijklmnopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_MultipleWrites_After()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test1", 8, true, writeCache, false);
			var write2 = FileWriterUtility.WriteToFile(tempFile, "Test2", 10, true, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdefghTest1ijTest2klmnopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_MultipleWrites_Before()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test1", 12, true, writeCache, false);
			var write2 = FileWriterUtility.WriteToFile(tempFile, "Test2", 6, true, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdefTest2ghijklTest1mnopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_MultipleWrites_SameOffset()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test1", 12, true, writeCache, false);
			var write2 = FileWriterUtility.WriteToFile(tempFile, "Test2", 12, true, writeCache, false);
			var write3 = FileWriterUtility.WriteToFile(tempFile, "Test3", 12, true, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdefghijklTest1Test2Test3mnopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_MultipleWrites_Random()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test1", 12, true, writeCache, false);
			var write2 = FileWriterUtility.WriteToFile(tempFile, "Test2", 2, true, writeCache, false);
			var write3 = FileWriterUtility.WriteToFile(tempFile, "Test3", 30, true, writeCache, false);
			var write4 = FileWriterUtility.WriteToFile(tempFile, "Test4", 20, true, writeCache, false);
			var write5 = FileWriterUtility.WriteToFile(tempFile, "Test5", 20, true, writeCache, false);
			var write6 = FileWriterUtility.WriteToFile(tempFile, "Test6", 21, true, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abTest2cdefghijklTest1mnopqrstTest4Test5uTest6vqxyz1234Test3567890", result);
		}

		[Test]
		public void ValidateText_MultipleWrites_Override()
		{
			var write = FileWriterUtility.WriteToFile(tempFile, "Test1", 8, false, writeCache, false); // Don't insert text
			var write2 = FileWriterUtility.WriteToFile(tempFile, "Test2", 10, true, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdefghTeTest2st1nopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void CheckWriteData_WriteRange_SameLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test", 4, 8, writeCache, false);

			Assert.AreEqual(4, write.bytesWritten);
			Assert.AreEqual(0, write.bytesAdded);
			Assert.AreEqual(4, write.offset);
		}

		[Test]
		public void CheckWriteData_WriteRange_LargerLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test", 6, 20, writeCache, false);

			Assert.AreEqual(4, write.bytesWritten);
			Assert.AreEqual(-10, write.bytesAdded);
			Assert.AreEqual(6, write.offset);
		}

		[Test]
		public void CheckWriteData_WriteRange_SmallerLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test", 6, 8, writeCache, false);

			Assert.AreEqual(4, write.bytesWritten);
			Assert.AreEqual(2, write.bytesAdded);
			Assert.AreEqual(6, write.offset);
		}

		[Test]
		public void ValidateText_WriteRange_SameLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test", 4, 8, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdTestijklmnopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_WriteRange_LargerLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test", 6, 20, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdefTestuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_WriteRange_SmallerLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test", 6, 8, writeCache, false);
			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdefTestijklmnopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_WriteRange_MultipleWrites_NoOverlap_SameFirstLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test1", 4, 9, writeCache, false);
			var write2 = FileWriterUtility.WriteToFileRange(tempFile, "Test2", 12, 13, writeCache, false);

			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdTest1jklTest2nopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_WriteRange_MultipleWrites_NoOverlap_SmallerFirstLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test1", 4, 5, writeCache, false);
			var write2 = FileWriterUtility.WriteToFileRange(tempFile, "Test2", 12, 13, writeCache, false);

			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdTest1fghijklTest2nopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_WriteRange_MultipleWrites_NoOverlap_LargerFirstLength()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test1", 4, 12, writeCache, false);
			var write2 = FileWriterUtility.WriteToFileRange(tempFile, "Test2", 14, 20, writeCache, false);

			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdTest1mnTest2uvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_WriteRange_MultipleWrites_Overlap()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test1", 4, 9, writeCache, false);
			var write2 = FileWriterUtility.WriteToFileRange(tempFile, "Test2", 8, 10, writeCache, false);

			var result = File.ReadAllText(tempFile);

			Assert.AreEqual("abcdTestTest2klmnopqrstuvqxyz1234567890", result);
		}

		[Test]
		public void ValidateText_WriteRange_MultipleWrites_Overlap_SmallerRange()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test1", 4, 6, writeCache, false);
			var write2 = FileWriterUtility.WriteToFileRange(tempFile, "Test2", 5, 7, writeCache, false);

			var result = File.ReadAllText(tempFile);

			Assert.Fail(); // TODO: This behaviour needs to be defined
		}

		[Test]
		public void ValidateText_WriteRange_MultipleWrites_Overlap_LargerRange_Inside()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test1", 4, 12, writeCache, false);
			var write2 = FileWriterUtility.WriteToFileRange(tempFile, "Test2", 5, 7, writeCache, false);

			var result = File.ReadAllText(tempFile);

			Assert.Fail(); // TODO: This behaviour should probably raise an error, since we're trying to write to bytes that were deleted
		}

		[Test]
		public void ValidateText_WriteRange_MultipleWrites_Overlap_LargerRange_Outside()
		{
			var write = FileWriterUtility.WriteToFileRange(tempFile, "Test1", 4, 12, writeCache, false);
			var write2 = FileWriterUtility.WriteToFileRange(tempFile, "Test2", 10, 11, writeCache, false);

			var result = File.ReadAllText(tempFile);

			Assert.Fail(); // TODO: This behaviour should probably raise an error, since we're trying to write to bytes that were deleted
		}
	}
}