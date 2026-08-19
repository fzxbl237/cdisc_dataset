using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace cdisc_dataset.Services;

public sealed class DialogHostResult : IDialogResult
{
    public DialogHostResult()
    {
    }

    public DialogHostResult(DialogButtonResult result)
    {
        Result = result;
    }

    public DialogHostResult(DialogButtonResult result, IDialogParameters parameters)
    {
        Result = result;
        Parameters = parameters;
    }

    public IDialogParameters? Parameters { get; set; } = new DialogParameters();

    public DialogButtonResult Result { get; set; }

    public DialogStatus Status { get; set; } = DialogStatus.Closed;
}
