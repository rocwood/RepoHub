using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Collections.Generic;

namespace RepoHub;

public partial class MessageBox
{
	[CascadingParameter] IMudDialogInstance MudDialog { get; set; }

	[Parameter] public string Title { get; set; }
	[Parameter] public string ContentText { get; set; }
	[Parameter] public string AdditionalInfo { get; set; }
	[Parameter] public Color Color { get; set; }
	[Parameter] public Severity Severity { get; set; }
	[Parameter] public List<DialogAction> Actions { get; set; }

	void Submit(object value)
	{
		MudDialog.Close(DialogResult.Ok(value));
	}

	void Cancel()
	{
		MudDialog.Cancel();
	}
}
