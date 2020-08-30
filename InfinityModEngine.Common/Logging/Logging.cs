using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;

namespace InfinityModEngine.Common.Logging
{
	public enum LogSeverity
	{
		Info,
		Warning,
		Error
	}

	public interface ILogger
	{
		public void Log(string message, LogSeverity severity);
	}

	public struct Log
	{
		public readonly DateTime date;
		public readonly string message;
		public readonly LogSeverity severity;

		public Log(string message, LogSeverity severity)
		{
			this.message = message;
			this.severity = severity;
			this.date = DateTime.Now;
		}
	}
}
