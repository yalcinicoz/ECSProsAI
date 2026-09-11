namespace ECSPros.Api.Services.AiReporting;

public static class OpenAiResponseLimits
{
    // Responses echoes instructions and the JSON schema outside generated output.
    // Bound that envelope separately; it is not the executable report proposal.
    public const int EnvelopeBytes = 1_048_576;
    public const int ProposalBytes = 65_536;
}
