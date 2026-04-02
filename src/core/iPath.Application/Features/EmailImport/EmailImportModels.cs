namespace iPath.Application.Features.EmailImport;

public record ImportEmailMessage(
    string MessageId,
    string Subject,
    string SenderEmail,
    string? SenderName,
    string? HtmlBody,
    string? PlainTextBody,
    DateTime ReceivedDate,
    IReadOnlyList<ImportEmailAttachment> Attachments
);

public record ImportEmailAttachment(
    string Filename,
    string ContentType,
    long Size,
    Stream Content
);

public record ImportEmailPreview(
    string MessageId,
    string Subject,
    string SenderEmail,
    string? SenderName,
    DateTime ReceivedDate,
    string? PreviewText,
    int AttachmentCount,
    IReadOnlyList<string> AttachmentNames
);

public record ImportEmailResult(
    bool Success,
    EmailImportStatus Status,
    Guid? ServiceRequestId,
    string? ErrorMessage,
    string MessageId
);

public record ImportMailboxSummary(
    string Name,
    int PendingCount,
    DateTime? LastChecked
);