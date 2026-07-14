using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;

namespace cdisc_dataset.Services.Interface;

public interface ICommentService
{
    Task<List<CommentDto>> GetAllSdtmCommentsAsync();
    
    Task<List<CommentDto>> GetAllCommentDtosAsync();
    
    Task<List<CommentDto>> GetAllCommentDtosWithoutErorrAsync();
    
    Task<bool> CommentExistsAsync(string commentUniqueId);
    
    Task<List<Comment>> GetAllCommentsWithoutErorrAsync();
    
    Task<List<Comment>> GetAllCommentsAsync();

    Task<Dictionary<string,string>> ConfirmCommentRefenceAsync(Comment? comment);
    
    Task<int> DeleteCommentAsync(Comment? comment);
    
    Task<Comment> InsertCommentAsync(Comment comment);
    
    Task<CommentDto> InsertCommentAsync(CommentDto commentDto);
    
    Task<Comment> UpdateCommentAsync(Comment comment);
    
    Task<Comment> UpdateCommentAsync(CommentDto comment);

    Task<int> SaveCommentsAsync(List<CommentDto> comments);
    
    List<CommentDto> GetAllSdtmComments();
}
