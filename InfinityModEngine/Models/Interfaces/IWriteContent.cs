﻿
namespace InfinityModEngine.Interfaces
{
	public interface IWriteContent
	{
		long StartOffset { get; }
		long? EndOffset { get; }
		string DataFilePath { get; }
		string Text { get; }
		bool Replace { get; }
	}
}