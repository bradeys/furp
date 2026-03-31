namespace Furp.Client.Authentication;

internal sealed class FurpServerOptions
{
    public const string SectionName = "FurpServer";
    public const string HttpClientName = "FurpServer";

    public string BaseUrl { get; set; } = "";
}
