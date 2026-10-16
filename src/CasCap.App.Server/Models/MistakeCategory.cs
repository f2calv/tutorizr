namespace CasCap.Models;

/// <summary>Kind of mistake the learner made.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MistakeCategory>))]
public enum MistakeCategory
{
    /// <summary>Case, gender, conjugation, tense or another grammatical form.</summary>
    Grammar,

    /// <summary>Wrong or unidiomatic word choice.</summary>
    Vocabulary,

    /// <summary>Spelling, capitalisation or umlaut.</summary>
    Spelling,

    /// <summary>Verb position or clause order.</summary>
    WordOrder,

    /// <summary>Pronunciation heard in a voice message.</summary>
    Pronunciation,

    /// <summary>Anything else.</summary>
    Other,
}
