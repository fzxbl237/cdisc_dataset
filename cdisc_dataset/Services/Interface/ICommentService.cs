using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Services.Interface;

public interface ICommentService
{
    Task<List<CommentDto>> GetAllSdtmCommentsAsync(int projectId);
    
    Task<List<CommentDto>> GetAllCommentDtosAsync(int projectId,CdiscDataType dataType);
    
    Task<List<CommentDto>> GetAllCommentDtosWithoutErorrAsync(int projectId, CdiscDataType dataType);
    
    Task<bool> CommentExistsAsync(int projectId, CdiscDataType dataType,string commentUniqueId);
    
    Task<List<Comment>> GetAllCommentsWithoutErorrAsync(int projectId,CdiscDataType dataType);
    
    Task<List<Comment>> GetAllCommentsAsync(int projectId,CdiscDataType dataType);

    Task<Dictionary<string,string>> ConfirmCommentRefenceAsync(Comment? comment);
    
    Task<int> DeleteCommentAsync(Comment? comment);
    
    Task<Comment> InsertCommentAsync(Comment comment);
    
    Task<CommentDto> InsertCommentAsync(CommentDto commentDto);
    
    Task<Comment> UpdateCommentAsync(Comment comment);
    
    Task<Comment> UpdateCommentAsync(CommentDto comment);

    Task<int> SaveCommentsAsync(List<CommentDto> comments);
    
    List<CommentDto> GetAllSdtmComments(int projectId);
}
