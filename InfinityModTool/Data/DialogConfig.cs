using InfinityModTool;
using System;
using System.Threading.Tasks;

public abstract class BaseDialogConfig
{
	private bool show;

	public AlertLevel level;
	public string title;
	public string message;

	public bool CanShow => show;

	public void Show()
	{
		this.show = true;
	}

	public void Hide()
	{
		this.show = false;
	}
}

public class AlertDialogConfig : BaseDialogConfig
{
	public string confirmButtonText;
	public Func<Task> confirmAction;
}

public class ConfirmationDialogConfig : BaseDialogConfig
{
	public string confirmButtonText;
	public string cancelButtonText;

	public Func<Task> confirmAction;
	public Func<Task> cancelAction;
}

public class ConfirmationDialogConfig<T> : ConfirmationDialogConfig
{
	new public Func<T, Task> confirmAction;
}

public class GenericDialogConfig : BaseDialogConfig
{
	public string[] buttonText;
	public Func<Task>[] buttonActions;
}