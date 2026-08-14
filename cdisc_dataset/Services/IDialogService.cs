using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using Prism.Dialogs;

namespace cdisc_dataset.Services;

public interface IDialogService
{
    Task<IDialogResult> ShowAddCommentModelAsync(string? defaultId = null);
    Task<IDialogResult> ShowEditCommentModelAsync(CommentDto comment);
    Task<IDialogResult> ShowAddDocumentModelAsync(DocumentDto? document = null);
    Task<IDialogResult> ShowEditDocumentModelAsync(DocumentDto document);
    Task<IDialogResult> ShowAddDictionaryModelAsync();
    Task<IDialogResult> ShowEditDictionaryModelAsync(DictionaryDto dictionary);
    Task<IDialogResult> ShowAddMethodModelAsync(MethodDto method);
    Task<IDialogResult> ShowEditMethodModelAsync(MethodDto method);
}
