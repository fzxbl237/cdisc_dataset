using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using Prism.Dialogs;

namespace cdisc_dataset.Services;

public sealed class DialogService(IDialogHostService dialogHostService) : IDialogService
{
    public Task<IDialogResult> ShowAddCommentModelAsync(string? defaultId = null)
    {
        var parameters = new DialogParameters
        {
            { "Title", "Add Comment" }
        };
        if (!string.IsNullOrWhiteSpace(defaultId))
            parameters.Add("DefaultId", defaultId);

        return dialogHostService.ShowDialogAsync("CommentDialog", parameters);
    }

    public Task<IDialogResult> ShowEditCommentModelAsync(CommentDto comment)
    {
        return dialogHostService.ShowDialogAsync("CommentDialog", new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "Model", comment }
        });
    }

    public Task<IDialogResult> ShowAddDocumentModelAsync(DocumentDto? document = null)
    {
        var parameters = new DialogParameters
        {
            { "Title", "Add Document" }
        };
        if (document is not null)
            parameters.Add("Model", document);

        return dialogHostService.ShowDialogAsync("DocumentDialog", parameters);
    }

    public Task<IDialogResult> ShowEditDocumentModelAsync(DocumentDto document)
    {
        return dialogHostService.ShowDialogAsync("DocumentDialog", new DialogParameters
        {
            { "Title", "Edit Document" },
            { "Model", document }
        });
    }

    public Task<IDialogResult> ShowAddDictionaryModelAsync()
    {
        return dialogHostService.ShowDialogAsync("DictionaryDialog", new DialogParameters
        {
            { "Title", "Add Dictionary" }
        });
    }

    public Task<IDialogResult> ShowEditDictionaryModelAsync(DictionaryDto dictionary)
    {
        return dialogHostService.ShowDialogAsync("DictionaryDialog", new DialogParameters
        {
            { "Title", "Modify Dictionary" },
            { "Model", dictionary }
        });
    }

    public Task<IDialogResult> ShowAddMethodModelAsync(MethodDto method)
    {
        return dialogHostService.ShowDialogAsync("MethodDialog", new DialogParameters
        {
            { "Title", "Add Method" },
            { "Model", method }
        });
    }

    public Task<IDialogResult> ShowEditMethodModelAsync(MethodDto method)
    {
        return dialogHostService.ShowDialogAsync("MethodDialog", new DialogParameters
        {
            { "Title", "Edit Method" },
            { "Model", method }
        });
    }
}
