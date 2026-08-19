using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace cdisc_dataset.Services;

public sealed class ReferenceDeletionService(
    ICommentService commentService,
    IMethodService methodService,
    IDialogHostService dialogHostService,
    IMapper mapper) : IReferenceDeletionService
{
    public async Task<bool?> ConfirmReferenceDeletionAsync(string title, string entityType, System.Collections.Generic.Dictionary<string, string> references)
    {
        var result = await dialogHostService.ShowDialogAsync("DeleteConfirmedDialog", new DialogParameters
        {
            { "Title", title },
            { "EntityType", entityType },
            { "References", references }
        });

        return result.Result switch
        {
            DialogButtonResult.Yes => false,
            DialogButtonResult.OK => true,
            _ => null
        };
    }

    public async Task<bool> ConfirmAndDeleteCommentAsync(Comment comment)
    {
        var clearReferences = await ConfirmReferenceDeletionAsync(
            $"Delete comment {comment.UniqueId}?",
            "Comment",
            await commentService.ConfirmCommentRefenceAsync(comment));
        if (clearReferences == null)
            return false;

        await commentService.DeleteCommentAsync(comment, clearReferences.Value);
        return true;
    }

    public async Task<bool> ConfirmAndDeleteMethodAsync(Method method)
    {
        var methodDto = mapper.Map<MethodDto>(method);
        var clearReferences = await ConfirmReferenceDeletionAsync(
            $"Delete method {method.UniqueId}?",
            "Method",
            await methodService.ConfirmMethodReferenceAsync(methodDto));
        if (clearReferences == null)
            return false;

        await methodService.DeleteMethodAsync(methodDto, clearReferences.Value);
        return true;
    }
}
