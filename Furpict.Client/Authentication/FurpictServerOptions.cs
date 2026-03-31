namespace Furpict.Client.Authentication;

internal sealed class FurpictServerOptions
{
    public const string SectionName = "FurpictServer";
    public const string HttpClientName = "FurpictServer";

    public string BaseUrl { get; set; } = "";
}
