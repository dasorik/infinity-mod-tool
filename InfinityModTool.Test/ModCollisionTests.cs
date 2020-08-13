using InfinityModTool.Data;
using InfinityModTool.Data.InstallActions;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Enums;
using InfinityModTool.Models;
using InfinityModTool.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace InfinityModTool.Test
{
	[TestFixture]
	class ModCollisionTests
	{
		string testFile1;
		string testFile2;
		string exampleFile1;
		string exampleFile2;
		string exampleFile3;
		string exampleDestination1;
		string exampleDestination2;
		string exampleDestination3_exampleDestination1;

		[SetUp]
		public void SetUp()
		{
			// Create some test files in a temp directory
			string path = Path.Combine(Global.APP_DATA_FOLDER, "TestTemp");
			Directory.CreateDirectory(path);

			testFile1 = Path.Combine(path, "Test1.txt");
			testFile2 = Path.Combine(path, "Test2.txt");
			exampleFile1 = Path.Combine(path, "ExampleFile1.txt");
			exampleFile2 = Path.Combine(path, "ExampleFile2.txt");
			exampleFile3 = Path.Combine(path, "ExampleFile3.txt");
			exampleDestination1 = Path.Combine(path, "ExampleDestination1.txt");
			exampleDestination2 = Path.Combine(path, "ExampleDestination2.txt");
			exampleDestination3_exampleDestination1 = Path.Combine(path, "ExampleDestination3.txt");

			File.WriteAllText(testFile1, "ascdefghijklmnopqrstuvqxyz");
			File.WriteAllText(testFile2, "1234567890");
			File.WriteAllText(exampleFile1, "ExampleText");
			File.WriteAllText(exampleFile2, "ExampleText2");
			File.WriteAllText(exampleFile3, "ExampleText3");

			// For when we're expecting data to read (we aren't moving these files around)
			File.WriteAllText(exampleDestination1, "DestinationText");
			File.WriteAllText(exampleDestination2, "DestinationText2");
			File.WriteAllText(exampleDestination3_exampleDestination1, "DestinationText");
		}

		[TearDown]
		public void TearDown()
		{
			string path = Path.Combine(Global.APP_DATA_FOLDER, "TestTemp");
			Directory.Delete(path, true);
		}

		// File Writes

		[Test]
		public void CheckFileWriteWithDelete()
		{
			var actions = new List<FileModification>()
			{
				new DeleteFileModification(exampleFile1, false, "Test.Delete")
			};

			var fileWriter = new FileWriterUtility();
			var content = new WriteToFileAction.WriteContent() { Text = "Test", StartOffset = 12 };
			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Edit" } };

			bool hasCollision = ModCollisionTracker.HasEditCollision(mod, exampleFile1, content, fileWriter, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Delete");
			Assert.IsTrue(collision.description.Contains("Attempting to write to a file that has been deleted by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileWriteWithMove()
		{
			var actions = new List<FileModification>()
			{
				new MoveFileModification(exampleFile1, exampleDestination1, false, "Test.Move")
			};

			var fileWriter = new FileWriterUtility();
			var content = new WriteToFileAction.WriteContent() { Text = "Test", StartOffset = 12 };
			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Edit" } };

			bool hasCollision = ModCollisionTracker.HasEditCollision(mod, exampleFile1, content, fileWriter, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Move");
			Assert.IsTrue(collision.description.Contains("Attempting to write to a file that has been moved by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileWriteWithReplace()
		{
			var actions = new List<FileModification>()
			{
				new ReplaceFileModification(exampleFile1, false, "Test.Move")
			};

			var fileWriter = new FileWriterUtility();
			var content = new WriteToFileAction.WriteContent() { Text = "Test", StartOffset = 12 };
			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Edit" } };

			bool hasCollision = ModCollisionTracker.HasEditCollision(mod, exampleFile1, content, fileWriter, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Move");
			Assert.IsTrue(collision.description.Contains("Attempting to write to a file that has been replaced by another mod"), $"Actual: {collision.description}");
		}


		// File Moves

		[Test]
		public void CheckFileMoveWithMove_DifferentTarget()
		{
			var actions = new List<FileModification>()
			{
				new MoveFileModification(exampleFile1, exampleDestination2, false, "Test.Move")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move2" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile1, exampleDestination1, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Move");
			Assert.IsTrue(collision.description.Contains("Attempting to move a file that has been moved by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileMoveWithMove_SameTarget()
		{
			var actions = new List<FileModification>()
			{
				new MoveFileModification(exampleFile1, exampleDestination1, false, "Test.Move")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move2" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile1, exampleDestination1, actions, out var collision);

			Assert.AreEqual(false, hasCollision);
		}

		[Test]
		public void CheckFileMoveWithMove_DiffSource_SameDest_DiffData()
		{
			var actions = new List<FileModification>()
			{
				new MoveFileModification(exampleFile1, exampleDestination1, false, "Test.Move"),
				new AddFileModification(exampleDestination1, false, "Test.Move"),
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move2" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile2, exampleDestination1, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Move");
			Assert.IsTrue(collision.description.Contains("Attempting to move a file to a destination that has been modified by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileMoveWithMove_DiffSource_SameDest_SameData()
		{
			var actions = new List<FileModification>()
			{
				new MoveFileModification(exampleFile1, exampleDestination1, false, "Test.Move"),
				new AddFileModification(exampleDestination1, false, "Test.Move"),
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move2" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleDestination3_exampleDestination1, exampleDestination1, actions, out var collision);

			Assert.AreEqual(false, hasCollision);
		}

		[Test]
		public void CheckFileMoveWithDelete()
		{
			var actions = new List<FileModification>()
			{
				new DeleteFileModification(exampleFile1, false, "Test.Delete")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile1, exampleDestination1, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Delete");
			Assert.IsTrue(collision.description.Contains("Attempting to move a file that has been deleted by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileMoveWithReplace()
		{
			var actions = new List<FileModification>()
			{
				new ReplaceFileModification(exampleFile1, false, "Test.Replace")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move2" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile1, exampleDestination1, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Replace");
			Assert.IsTrue(collision.description.Contains("Attempting to move a file that has been replaced by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileMoveWithWrite()
		{
			var actions = new List<FileModification>()
			{
				new EditFileModification(exampleFile1, false, "Test.Edit")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move2" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile1, exampleDestination1, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Edit");
			Assert.IsTrue(collision.description.Contains("Attempting to move a file that has been written to by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileMoveWithAdd_SameDest()
		{
			var actions = new List<FileModification>()
			{
				new AddFileModification(exampleFile1, false, "Test.Add")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile2, exampleFile1, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Add");
			Assert.IsTrue(collision.description.Contains("Attempting to move a file to a destination that has been modified by another mod (with different data)"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileMoveWithAdd_DiffDest()
		{
			var actions = new List<FileModification>()
			{
				new AddFileModification(exampleFile2, false, "Test.Add")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Move" } };
			bool hasCollision = ModCollisionTracker.HasMoveCollision(mod, exampleFile3, exampleFile1, actions, out var collision);

			Assert.AreEqual(false, hasCollision);
		}

		// File Replacements

		[Test]
		public void CheckFileReplaceWithMove()
		{
			var actions = new List<FileModification>()
			{
				new MoveFileModification(exampleFile1, "C:\\Example\\ExampleFile.txt", false, "Test.Move")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Replace" } };
			bool hasCollision = ModCollisionTracker.HasReplaceCollision(mod, exampleFile1, exampleFile2, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Move");
			Assert.IsTrue(collision.description.Contains("Attempting to replace a file that has been moved by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileReplaceWithDelete()
		{
			var actions = new List<FileModification>()
			{
				new DeleteFileModification(exampleFile1, false, "Test.Delete")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Replace" } };
			bool hasCollision = ModCollisionTracker.HasReplaceCollision(mod, exampleFile1, exampleFile2, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Delete");
			Assert.IsTrue(collision.description.Contains("Attempting to replace a file that has been deleted by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileReplaceWithWrite()
		{
			var actions = new List<FileModification>()
			{
				new EditFileModification(exampleFile1, false, "Test.Edit")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Replace" } };
			bool hasCollision = ModCollisionTracker.HasReplaceCollision(mod, exampleFile1, exampleFile2, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Edit");
			Assert.IsTrue(collision.description.Contains("Attempting to replace a file that has been written to by another mod"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileReplaceWithReplace_DifferentData()
		{
			var actions = new List<FileModification>()
			{
				new ReplaceFileModification(exampleFile1, false, "Test.Replace1")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Replace2" } };
			bool hasCollision = ModCollisionTracker.HasReplaceCollision(mod, exampleFile1, testFile2, actions, out var collision);

			Assert.AreEqual(true, hasCollision);
			Assert.AreEqual(collision.severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collision.modID, "Test.Replace1");
			Assert.IsTrue(collision.description.Contains("Attempting to replace a file that has been replaced by another mod (with different data)"), $"Actual: {collision.description}");
		}

		[Test]
		public void CheckFileReplaceWithReplace_SameData()
		{
			var actions = new List<FileModification>()
			{
				new ReplaceFileModification(exampleDestination1, false, "Test.Replace1")
			};

			var mod = new GameModification() { Config = new Data.BaseModConfiguration() { ModID = "Test.Replace2" } };
			bool hasCollision = ModCollisionTracker.HasReplaceCollision(mod, exampleFile1, exampleDestination3_exampleDestination1, actions, out var collision);

			Assert.AreEqual(false, hasCollision);
		}

		// File Copies

		//[Test]
		//public void CheckFileCopyWithCopy_SameSourceFile_DiffDest()
		//{
		//	var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileCopy_exampleFile_assets });
		//	var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

		//	Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		//}

		//[Test]
		//public void CheckFileCopyWithCopy_DiffSourceFile_SameDest()
		//{
		//	var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileCopy_exampleFile2_gameDB });
		//	var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

		//	Assert.Greater(collisions.Length, 0, "No collisions were detected");
		//	Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
		//	Assert.AreEqual(collisions[0].mod, fileCopy_exampleFile2_gameDB);
		//	Assert.AreEqual("Attempting to copy a file to a destination that is copied to by another mod (with different data)", collisions[0].description);
		//}

		//[Test]
		//public void CheckFileCopyWithMove_SameDest()
		//{
		//	var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileMove_exampleFile2_gamedb });
		//	var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

		//	Assert.Greater(collisions.Length, 0, "No collisions were detected");
		//	Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
		//	Assert.AreEqual(collisions[0].mod, fileMove_exampleFile2_gamedb);
		//	Assert.AreEqual("Attempting to copy a file to a destination that is moved to by another mod (with different data)", collisions[0].description);
		//}

		//[Test]
		//public void CheckFileCopyWithMove_DiffDest()
		//{
		//	var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileMove_exampleFile_differentDest });
		//	var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

		//	Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		//}
	}
}
