namespace V2rayWatch.Server.Web;

public sealed class BaleNotificationSettings
{
    public bool Enabled { get; set; }
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
}
