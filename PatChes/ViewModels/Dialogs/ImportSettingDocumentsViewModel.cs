using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using PatChes.Services;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Dialogs;

public partial class ImportSettingDocumentsViewModel(IDocumentService documentService) : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; } = "Root";

    [ObservableProperty]
    private List<IListItemData> _documents = [];

    [ObservableProperty]
    private ObservableCollection<EntityKey> _targetKeys = [];

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        var documents = await documentService.GetAvailableSettingDocumentsAsync();
        Documents = documents
            .OrderBy(document => document.UniqueId)
            .Select(document => (IListItemData)new ListItemData
            {
                ItemKey = document.Id.ToString(),
                Content = $"{document.UniqueId} - {document.Title} ({document.Href})"
            })
            .ToList();
        TargetKeys.Clear();
    }

    [RelayCommand]
    private void Save()
    {
        var result = new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters
            {
                {
                    "TemplateDocumentIds",
                    TargetKeys
                        .Select(key => int.TryParse(key.Value, out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList()
                }
            }
        };
        DialogHost.Close(DialogHostName ?? "Root", result);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}
