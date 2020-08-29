using InfinityModFramework.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace InfinityModFramework.Interfaces
{
	public interface IGameIntegration
	{
		public QuickBMSAutoMappingCollection GetQuickBMSAutomappings();
		public string[] GetReservedFiles();
	}
}
