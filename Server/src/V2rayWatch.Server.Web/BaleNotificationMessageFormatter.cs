namespace V2rayWatch.Server.Web;

public static class BaleNotificationMessageFormatter
{
    public static string Format(int profileNumber, string currentStatus)
    {
        if (profileNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(profileNumber));

        var color = currentStatus switch
        {
            "Disconnected" => "red",
            "Connected" => "green",
            _ => throw new ArgumentOutOfRangeException(nameof(currentStatus))
        };

        return $"no {profileNumber} {color}";
    }
}
