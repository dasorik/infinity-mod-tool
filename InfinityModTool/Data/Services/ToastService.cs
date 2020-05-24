using InfinityModTool.Shared;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InfinityModTool.Services
{
	public class ToastService
	{
		private Dictionary<Guid, ToastPopup> activeInstances;

		public void ShowToast(string message, AlertLevel level, decimal? activeTime = null)
		{
			var toast = new ToastPopup
			{
				Message = message,
				Type = level,
				ActiveTime = activeTime
			};

			activeInstances.Add(toast.ID, toast);
		}

		public void RemoveToast(ToastPopup popup)
		{
			activeInstances.Remove(popup.ID);
		}
	}
}
