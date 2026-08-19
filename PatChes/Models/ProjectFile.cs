using System;
using PatChes.Models.Enums;
using LiteDB;
using SqlSugar;

namespace PatChes.Models;

[Tenant("project")]
public class ProjectFile
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public int ProjectId { get; set; }

    public ProjectFileType FileType { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    public ObjectId StorageId { get; set; }
}
