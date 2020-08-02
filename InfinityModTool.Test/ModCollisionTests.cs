using InfinityModTool.Data.InstallActions;
using InfinityModTool.Data.Modifications;
using InfinityModTool.Enums;
using InfinityModTool.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace InfinityModTool.Test
{
	[TestFixture]
	class ModCollisionTests
	{
		GameModification fileWrite_exampleFile0x004543 = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileWriteAction()
					{
						Action = "WriteToFile",
						TargetFile = "[Game]/presentation/exampleFile.lua",
						Content = new[]
						{
							new FileWriteAction.WriteContent()
							{
								Text = "Hello World",
								StartOffset = 0x004543,
								Replace = false
							}
						}
					}
				}
			}
		};

		GameModification fileDelete_exampleFile = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileDeleteAction()
					{
						Action = "DeleteFiles",
						TargetFiles = new[] { "[Game]/presentation/exampleFile.lua" }
					}
				}
			}
		};

		GameModification fileMove_exampleFile = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileMoveAction()
					{
						Action = "MoveFile",
						TargetFile = "[Game]/presentation/exampleFile.lua",
						DestinationPath = "[Game]/presentation2/exampleFile.lua"
					}
				}
			}
		};

		GameModification fileMove_exampleFile2_toPresentation2 = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileMoveAction()
					{
						Action = "MoveFile",
						TargetFile = "[Game]/presentation/exampleFile2.lua",
						DestinationPath = "[Game]/presentation2/exampleFile.lua"
					}
				}
			}
		};

		GameModification fileMove_exampleFile2_gamedb = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileMoveAction()
					{
						Action = "MoveFile",
						TargetFile = "[Game]/presentation/exampleFile2.lua",
						DestinationPath = "[Game]/gamedb/exampleFile.lua"
					}
				}
			}
		};

		GameModification fileMove_exampleFile_differentDest = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileMoveAction()
					{
						Action = "MoveFile",
						TargetFile = "[Game]/presentation/exampleFile.lua",
						DestinationPath = "[Game]/presentation2/different.lua"
					}
				}
			}
		};

		GameModification fileReplace_exampleFile = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileReplaceAction()
					{
						Action = "ReplaceFile",
						TargetFile = "[Game]/presentation/exampleFile.lua",
						ReplacementFile = "[Game]/presentation2/exampleFile.lua"
					}
				}
			}
		};

		GameModification fileCopy_exampleFile_gameDB = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileCopyAction()
					{
						Action = "CopyFile",
						TargetFile = "[Game]/presentation/exampleFile.lua",
						DestinationPath = "[Game]/gamedb/exampleFile.lua"
					}
				}
			}
		};

		GameModification fileCopy_exampleFile_assets = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileCopyAction()
					{
						Action = "CopyFile",
						TargetFile = "[Game]/presentation/exampleFile.lua",
						DestinationPath = "[Game]/assets/exampleFile.lua"
					}
				}
			}
		};

		GameModification fileCopy_exampleFile2_gameDB = new GameModification()
		{
			Config = new Data.BaseModConfiguration()
			{
				InstallActions = new ModInstallAction[]
				{
					new FileCopyAction()
					{
						Action = "CopyFile",
						TargetFile = "[Game]/presentation/exampleFile2.lua",
						DestinationPath = "[Game]/gamedb/exampleFile.lua"
					}
				}
			}
		};

		ModCollisionTracker tracker;

		[SetUp]
		public void SetUp()
		{
			tracker = new ModCollisionTracker(new Data.Configuration() { });
		}

		// File Writes

		[Test]
		public void CheckFileWriteWithDelete()
		{
			var actions = ModUtility.GetModActions(new[] { fileWrite_exampleFile0x004543, fileDelete_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileWrite_exampleFile0x004543, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileDelete_exampleFile);
			Assert.AreEqual("Attempting to write to a file that is deleted by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileWriteWithMove()
		{
			var actions = ModUtility.GetModActions(new[] { fileWrite_exampleFile0x004543, fileMove_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileWrite_exampleFile0x004543, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileMove_exampleFile);
			Assert.AreEqual("Attempting to write to a file that is moved by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileWriteWithReplace()
		{
			var actions = ModUtility.GetModActions(new[] { fileWrite_exampleFile0x004543, fileReplace_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileWrite_exampleFile0x004543, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileReplace_exampleFile);
			Assert.AreEqual("Attempting to write to a file that is replaced by another mod", collisions[0].description);
		}


		// File Deletes

		[Test]
		public void CheckFileDeleteWithWrite()
		{
			var actions = ModUtility.GetModActions(new[] { fileDelete_exampleFile, fileWrite_exampleFile0x004543 });
			var collisions = tracker.CheckForPotentialModCollisions(fileDelete_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileWrite_exampleFile0x004543);
			Assert.AreEqual("Attempting to delete a file that is written to by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileDeleteWithReplace()
		{
			var actions = ModUtility.GetModActions(new[] { fileDelete_exampleFile, fileReplace_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileDelete_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileReplace_exampleFile);
			Assert.AreEqual("Attempting to delete a file that is replaced by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileDeleteWithMove()
		{
			var actions = ModUtility.GetModActions(new[] { fileDelete_exampleFile, fileMove_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileDelete_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Warning);
			Assert.AreEqual(collisions[0].mod, fileMove_exampleFile);
			Assert.AreEqual("Attempting to delete a file that is moved by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileDeleteWithDelete()
		{
			var actions = ModUtility.GetModActions(new[] { fileDelete_exampleFile, fileDelete_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileDelete_exampleFile, actions);

			Assert.AreEqual(0, collisions.Length, "Collisions were detected");
		}

		// File Moves

		[Test]
		public void CheckFileMoveWithMove_DifferentTarget()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile, fileMove_exampleFile_differentDest });
			var collisions = tracker.CheckForPotentialModCollisions(fileMove_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileMove_exampleFile_differentDest);
			Assert.AreEqual("Attempting to move a file that is moved elsewhere by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileMoveWithMove_SameTarget()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile, fileMove_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileDelete_exampleFile, actions);

			Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		}

		[Test]
		public void CheckFileMoveWithMove_DiffSource_SameDest()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile, fileMove_exampleFile2_toPresentation2 });
			var collisions = tracker.CheckForPotentialModCollisions(fileMove_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileMove_exampleFile2_toPresentation2);
			Assert.AreEqual("Attempting to move a file to a destination that is moved to by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileMoveWithDelete()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile, fileDelete_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileMove_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Warning);
			Assert.AreEqual(collisions[0].mod, fileDelete_exampleFile);
			Assert.AreEqual("Attempting to move a file that is deleted by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileMoveWithReplace()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile, fileReplace_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileMove_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileReplace_exampleFile);
			Assert.AreEqual("Attempting to move a file that is replaced by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileMoveWithWrite()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile, fileWrite_exampleFile0x004543 });
			var collisions = tracker.CheckForPotentialModCollisions(fileMove_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileWrite_exampleFile0x004543);
			Assert.AreEqual("Attempting to move a file that is written to by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileMoveWithCopy_SameDest()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile2_gamedb, fileCopy_exampleFile_gameDB });
			var collisions = tracker.CheckForPotentialModCollisions(fileMove_exampleFile2_gamedb, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileCopy_exampleFile_gameDB);
			Assert.AreEqual("Attempting to move a file to a destination that is copied to by another mod (with different data)", collisions[0].description);
		}

		[Test]
		public void CheckFileMoveWithCopy_DiffDest()
		{
			var actions = ModUtility.GetModActions(new[] { fileMove_exampleFile_differentDest, fileCopy_exampleFile_gameDB });
			var collisions = tracker.CheckForPotentialModCollisions(fileMove_exampleFile_differentDest, actions);

			Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		}

		// File Replacements

		[Test]
		public void CheckFileReplaceWithMove()
		{
			var actions = ModUtility.GetModActions(new[] { fileReplace_exampleFile, fileMove_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileReplace_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileMove_exampleFile);
			Assert.AreEqual("Attempting to replace a file that is moved by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileReplaceWithDelete()
		{
			var actions = ModUtility.GetModActions(new[] { fileReplace_exampleFile, fileDelete_exampleFile });
			var collisions = tracker.CheckForPotentialModCollisions(fileReplace_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileDelete_exampleFile);
			Assert.AreEqual("Attempting to replace a file that is deleted by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileReplaceWithWrite()
		{
			var actions = ModUtility.GetModActions(new[] { fileReplace_exampleFile, fileWrite_exampleFile0x004543 });
			var collisions = tracker.CheckForPotentialModCollisions(fileReplace_exampleFile, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileWrite_exampleFile0x004543);
			Assert.AreEqual("Attempting to replace a file that is written to by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileReplaceWithReplace_DifferentData()
		{
			Assert.Fail();
			//var actions = ModUtility.GetModActions(new[] { fileReplace_exampleFile, fileReplace_exampleFile });
			//var collisions = tracker.CheckForPotentialModCollisions(fileReplace_exampleFile, actions);

			//Assert.Greater(collisions.Length, 0, "No collisions were detected");
			//Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			//Assert.AreEqual(collisions[0].mod, fileReplace_exampleFile);
			//Assert.AreEqual("Attempting to replace a file that is deleted by another mod", collisions[0].description);
		}

		[Test]
		public void CheckFileReplaceWithReplace_SameData()
		{
			Assert.Fail();
			//var actions = ModUtility.GetModActions(new[] { fileReplace_exampleFile, fileReplace_exampleFile });
			//var collisions = tracker.CheckForPotentialModCollisions(fileDelete_exampleFile, actions);

			//Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		}

		// File Copies

		[Test]
		public void CheckFileCopyWithCopy_SameFile()
		{
			var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileCopy_exampleFile_gameDB });
			var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

			Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		}

		[Test]
		public void CheckFileCopyWithCopy_SameSourceFile_DiffDest()
		{
			var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileCopy_exampleFile_assets });
			var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

			Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		}

		[Test]
		public void CheckFileCopyWithCopy_DiffSourceFile_SameDest()
		{
			var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileCopy_exampleFile2_gameDB });
			var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileCopy_exampleFile2_gameDB);
			Assert.AreEqual("Attempting to copy a file to a destination that is copied to by another mod (with different data)", collisions[0].description);
		}

		[Test]
		public void CheckFileCopyWithMove_SameDest()
		{
			var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileMove_exampleFile2_gamedb });
			var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

			Assert.Greater(collisions.Length, 0, "No collisions were detected");
			Assert.AreEqual(collisions[0].severity, ModCollisionSeverity.Clash);
			Assert.AreEqual(collisions[0].mod, fileMove_exampleFile2_gamedb);
			Assert.AreEqual("Attempting to copy a file to a destination that is moved to by another mod (with different data)", collisions[0].description);
		}

		[Test]
		public void CheckFileCopyWithMove_DiffDest()
		{
			var actions = ModUtility.GetModActions(new[] { fileCopy_exampleFile_gameDB, fileMove_exampleFile_differentDest });
			var collisions = tracker.CheckForPotentialModCollisions(fileCopy_exampleFile_gameDB, actions);

			Assert.AreEqual(collisions.Length, 0, "Collisions were detected");
		}
	}
}
