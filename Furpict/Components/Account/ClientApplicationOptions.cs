namespace Furpict.Components.Account;

internal sealed class ClientApplicationOptions
{
    public const string SectionName = "ClientApplications";

    public string[] AllowedOrigins { get; set; } = [];
}
