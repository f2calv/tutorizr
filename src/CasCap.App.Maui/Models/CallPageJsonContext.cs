using System.Text.Json.Serialization;

namespace CasCap.Models;

/// <summary>Source-generated JSON metadata for the call page's JavaScript interop.</summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
internal sealed partial class CallPageJsonContext : JsonSerializerContext;
