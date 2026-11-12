namespace CasCap.Models;

/// <summary>Common European Framework of Reference language level.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CefrLevel>))]
public enum CefrLevel
{
    /// <summary>Beginner.</summary>
    A1,

    /// <summary>Elementary.</summary>
    A2,

    /// <summary>Intermediate.</summary>
    B1,

    /// <summary>Upper intermediate.</summary>
    B2,

    /// <summary>Advanced.</summary>
    C1,

    /// <summary>Proficient.</summary>
    C2,
}
