namespace Secms.Application.Interfaces;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string preferredFileName, string contentType, string folder, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

public record StoredFile(string StoragePath, string FileName, string ContentType, long SizeBytes, string? PublicUrl);

public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAdmin { get; }
    bool IsTeacher { get; }
    Guid? TeacherProfileId { get; }
}
