namespace InvestindoEmNegocio.Application.Services;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
    public string ApiBaseUrl { get; set; } = "https://api.anthropic.com";
    public string ApiVersion { get; set; } = "2023-06-01";
    public int MaxTokens { get; set; } = 1024;
}
