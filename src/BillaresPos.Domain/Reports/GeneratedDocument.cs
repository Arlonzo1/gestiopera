using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Reports;

public enum GeneratedDocumentStatus
{
    Pending,
    Generated,
    Failed
}

public sealed class GeneratedDocument : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public byte[]? Content { get; set; }
    public GeneratedDocumentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? GeneratedAt { get; set; }
    public string? Error { get; set; }
}
