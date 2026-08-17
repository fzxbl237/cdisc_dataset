using System.Windows.Input;
using Prism.Commands;
using Prism.Dialogs;
using IDialogAware = AsyncNavigation.Abstractions.IDialogAware;

namespace cdisc_dataset.Services;

public interface IDialogHostAware
{
    /// <summary>
    /// DialoHost名称
    /// </summary>
    string? DialogHostName { get; set; }

    /// <summary>
    /// 打开过程中执行
    /// </summary>
    /// <param name="parameters"></param>
    void OnDialogOpened(IDialogParameters parameters);
    
}