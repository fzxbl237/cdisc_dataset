using cdisc_dataset.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class DeleteCommentViewModel:ObservableObject,IDialogHostAware
{
    public string DialogHostName { get; set; } = "Root";
    
    [ObservableProperty]
    private string _title = "Remove references and delete comment?";

    [ObservableProperty] 
    [NotifyPropertyChangedFor("IsShowDatasets")]
    private string? _datasets;

    public bool IsShowDatasets => !string.IsNullOrWhiteSpace(Datasets);
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor("IsShowVariables")]
    private string? _variables;

    public bool IsShowVariables => !string.IsNullOrWhiteSpace(Variables);
    
    
    public void OnDialogOpened(IDialogParameters parameters)
    {
        parameters.TryGetValue("CommentUniqueId", out string commentUniqueId);
        parameters.TryGetValue("Variables", out string variables);
        parameters.TryGetValue("Datasets", out string datasets);
        if (!string.IsNullOrWhiteSpace(variables))
        {
            Variables =  variables;
        }
        if (!string.IsNullOrWhiteSpace(datasets))
        {
            Datasets =  datasets;
        }
        if (string.IsNullOrWhiteSpace(commentUniqueId))
        {
            Title = $"Remove references and delete {commentUniqueId}?";
        }
    }
    
    [RelayCommand]
    private void Save()
    {
        DialogHost.Close("Root",new DialogResult(ButtonResult.OK) );
    }
    
    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close("Root",new DialogResult{Result = ButtonResult.Cancel} );
    }
}