namespace CasCap.Models;

/// <summary>The learner's stored estimated level.</summary>
/// <param name="EstimatedLevel">Current estimated level, if one has been set.</param>
/// <param name="UpdatedUtc">When the estimated level was last set.</param>
public sealed record LearnerLevel(CefrLevel? EstimatedLevel, DateTimeOffset? UpdatedUtc);
