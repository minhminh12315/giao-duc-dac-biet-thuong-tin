# 10 — File Storage

## 1. Principle

DB **không** lưu binary. Chỉ lưu metadata + `storage_path` (relative key) và optional `public_url`.

## 2. Abstraction

```csharp
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string preferredFileName, string contentType, string folder, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct);
    Task DeleteAsync(string storagePath, CancellationToken ct);
}

public record StoredFile(string StoragePath, string FileName, string ContentType, long SizeBytes, string? PublicUrl);
```

Implementations:
- `LocalFileStorage` — root `App_Data/uploads`
- `S3FileStorage` — phase prod

## 3. Path convention

```
teachers/{teacherProfileId}/{yyyy}/{MM}/{guid}_{safeFileName}
students/{studentId}/{yyyy}/{MM}/{guid}_{safeFileName}
```

`safeFileName`: strip path chars, max 100 chars.

## 4. Validation

| Rule | Value |
|------|-------|
| Max size | 10 MB |
| Allowed MIME | image/jpeg, image/png, image/webp, application/pdf |
| Magicheck | optional: verify file header matches MIME |

Reject executable types.

## 5. Document types

**Teacher:** Contract, Certificate, Other  
**Student:** MedicalReport, Assessment, Other

## 6. Security

- Download qua API có authz (không expose raw disk path)
- Teacher chỉ xem document của mình; Admin full
- Teacher không xem student medical docs trừ khi policy mở (MVP: **chỉ Admin** xem student docs; Teacher chỉ xem progress comments)
- Virus scan: out of MVP

## 7. API upload

`POST multipart/form-data`

Fields: `file`, `documentType`

Response:

```json
{
  "id": "uuid",
  "fileName": "hop-dong.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 12345,
  "storagePath": "teachers/.../file.pdf",
  "uploadedAt": "..."
}
```

## 8. EF entity mapping

Map đúng bảng `teacher_documents` / `student_documents` trong `04_DATABASE_SCHEMA.md`.
