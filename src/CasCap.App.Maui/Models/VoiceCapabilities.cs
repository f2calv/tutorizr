namespace CasCap.Models;

// TODO: Use Agentizr's shared response contract once the pinned package includes it.
internal sealed record VoiceCapabilities
{
    public required bool VoiceEnabled { get; init; }
    public required bool KnowledgeToolsEnabled { get; init; }
    public required bool RetentionAllowed { get; init; }
}
