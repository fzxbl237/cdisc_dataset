using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Models;

namespace PatChes.Services.Interface;

public interface IReferenceDeletionService
{
    Task<bool?> ConfirmReferenceDeletionAsync(string title, string entityType, System.Collections.Generic.Dictionary<string, string> references);
    Task<bool> ConfirmAndDeleteCommentAsync(Comment comment);
    Task<bool> ConfirmAndDeleteMethodAsync(Method method);
}
