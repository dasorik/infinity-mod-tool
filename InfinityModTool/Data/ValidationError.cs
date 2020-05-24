using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfinityModTool.Data
{
	public enum ValidationSeverity
	{
		None,
		Warning,
		Error
	}

	public class ValidationResponse
	{
		public ValidationSeverity Type { get; }
		public string Message { get; }

		public ValidationResponse(ValidationSeverity type)
			: this(type, "")
		{
		}

		public ValidationResponse(ValidationSeverity type, string message)
		{
			this.Type = type;
			this.Message = message;
		}

		public bool IsError => Type == ValidationSeverity.Error || Type == ValidationSeverity.Warning;
	}
}
