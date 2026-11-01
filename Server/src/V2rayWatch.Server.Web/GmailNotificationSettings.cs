namespace V2rayWatch.Server.Web;

public sealed class GmailNotificationSettings
{
    public bool Enabled { get; set; }
    public string SenderEmail { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public string AppPassword { get; set; } = "";
    public string? EgressProfileId { get; set; }
}
