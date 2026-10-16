namespace CasCap.Tests.Unit;

public sealed class TutorMcpToolAnnotationTests
{
    //Pinned so a hint change is a deliberate, reviewed edit: agentizr withholds non-read-only tools unless a definition names them.
    private static readonly Dictionary<string, (bool ReadOnly, bool Destructive, bool Idempotent)> Expected = new()
    {
        [nameof(TutorMcpQueryService.GetLearnerProfile)] = (true, false, true),
        [nameof(TutorMcpQueryService.SetLearnerLevel)] = (false, false, true),
        [nameof(TutorMcpQueryService.AddVocabulary)] = (false, false, true),
        [nameof(TutorMcpQueryService.GetDueVocabulary)] = (true, false, true),
        [nameof(TutorMcpQueryService.RecordVocabularyReview)] = (false, false, false),
        [nameof(TutorMcpQueryService.RecordMistake)] = (false, false, false),
        [nameof(TutorMcpQueryService.GetRecentMistakes)] = (true, false, true),
    };

    [Fact]
    [Trait("Category", "Tools")]
    public void ToolHints()
    {
        var tools = typeof(TutorMcpQueryService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => (method.Name, Attribute: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(tool => tool.Attribute is not null)
            .ToDictionary(tool => tool.Name, tool => (tool.Attribute!.ReadOnly, tool.Attribute.Destructive, tool.Attribute.Idempotent));

        Assert.Equal(Expected.OrderBy(pair => pair.Key), tools.OrderBy(pair => pair.Key));
    }

    [Fact]
    [Trait("Category", "Tools")]
    public void ToolsAreClosedWorldWithStructuredContent()
    {
        var attributes = typeof(TutorMcpQueryService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .OfType<McpServerToolAttribute>()
            .ToList();

        Assert.NotEmpty(attributes);
        Assert.All(attributes, attribute =>
        {
            Assert.False(attribute.OpenWorld);
            Assert.True(attribute.UseStructuredContent);
        });
    }
}
