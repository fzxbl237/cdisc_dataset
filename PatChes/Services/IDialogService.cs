using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.Services;

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
