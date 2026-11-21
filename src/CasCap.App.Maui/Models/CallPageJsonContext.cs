using System.Text.Json.Serialization;

namespace CasCap.Models;

/// <summary>Source-generated JSON metadata for voice interop and capability checks.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(VoiceCapabilities))]
internal sealed partial class CallPageJsonContext : JsonSerializerContext;
