using Pehlione.Models.Identity;

namespace Pehlione.Models.Communication;

public sealed class CustomerRelationsMessage
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
