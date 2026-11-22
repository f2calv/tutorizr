using StackExchange.Redis;

namespace CasCap.TestData;

/// <summary>Offline Redis command fixture; it does not model network failures or transaction atomicity.</summary>
internal sealed class RedisLearnerTestData
{
    private readonly Dictionary<string, Dictionary<string, RedisValue>> _hashes = [];
    private readonly Dictionary<string, Dictionary<RedisValue, double>> _sets = [];

    /// <summary>Keys acted on by data commands, excluding connection setup.</summary>
    public HashSet<string> Keys { get; } = [];

    /// <summary>Builds a connection whose database and transactions share this fixture's records.</summary>
    public IConnectionMultiplexer CreateConnection() => CreateProxy<IConnectionMultiplexer>();

    private T CreateProxy<T>() where T : class
    {
        var proxy = DispatchProxy.Create<T, RedisProxy>();
        ((RedisProxy)(object)proxy).Fixture = this;
        return proxy;
    }

    private object Invoke(MethodInfo method, object?[] arguments)
    {
        if (method.Name == nameof(IConnectionMultiplexer.GetDatabase))
            return CreateProxy<IDatabase>();
        if (method.Name == nameof(IDatabase.CreateTransaction))
            return CreateProxy<ITransaction>();
        if (method.Name == nameof(ITransaction.ExecuteAsync))
            return Task.FromResult(true);

        var key = ((RedisKey)arguments[0]!).ToString();
        Keys.Add(key);
        if (!_hashes.TryGetValue(key, out var hash))
            _hashes[key] = hash = [];
        if (!_sets.TryGetValue(key, out var set))
            _sets[key] = set = [];

        switch (method.Name)
        {
            case nameof(IDatabase.HashGetAsync):
                return arguments[1] is RedisValue[] fields
                    ? Task.FromResult(fields.Select(field => hash.GetValueOrDefault(field.ToString())).ToArray())
                    : Task.FromResult(hash.GetValueOrDefault(((RedisValue)arguments[1]!).ToString()));
            case nameof(IDatabase.HashSetAsync):
                if (arguments[1] is HashEntry[] entries)
                {
                    foreach (var entry in entries)
                        hash[entry.Name.ToString()] = entry.Value;
                    return Task.CompletedTask;
                }
                hash[((RedisValue)arguments[1]!).ToString()] = (RedisValue)arguments[2]!;
                return Task.FromResult(true);
            case nameof(IDatabase.HashLengthAsync):
                return Task.FromResult((long)hash.Count);
            case nameof(IDatabase.SortedSetAddAsync):
                set[(RedisValue)arguments[1]!] = (double)arguments[2]!;
                return Task.FromResult(true);
            case nameof(IDatabase.SortedSetLengthAsync):
                return Task.FromResult((long)set.Count(pair =>
                    pair.Value >= (double)arguments[1]! && pair.Value <= (double)arguments[2]!));
            case nameof(IDatabase.SortedSetRangeByRankAsync):
                {
                    var values = (Order)arguments[3]! == Order.Descending
                        ? set.OrderByDescending(pair => pair.Value)
                        : set.OrderBy(pair => pair.Value);
                    var start = (long)arguments[1]!;
                    var stop = (long)arguments[2]!;
                    return Task.FromResult(values.Skip((int)start).Take((int)(stop - start + 1)).Select(pair => pair.Key).ToArray());
                }
            case nameof(IDatabase.SortedSetRangeByScoreAsync):
                return Task.FromResult(set.Where(pair => pair.Value >= (double)arguments[1]!
                    && pair.Value <= (double)arguments[2]!).OrderBy(pair => pair.Value)
                    .Skip((int)(long)arguments[5]!).Take((int)(long)arguments[6]!).Select(pair => pair.Key).ToArray());
            case nameof(IDatabase.SortedSetRemoveRangeByRankAsync):
                {
                    var start = (long)arguments[1]!;
                    var stop = (long)arguments[2]!;
                    if (stop < 0)
                        stop += set.Count;
                    var removed = set.OrderBy(pair => pair.Value).Skip((int)start)
                        .Take((int)Math.Max(0, stop - start + 1)).Select(pair => pair.Key).ToArray();
                    foreach (var value in removed)
                        set.Remove(value);
                    return Task.FromResult((long)removed.Length);
                }
            default:
                throw new NotSupportedException($"Redis fixture does not implement {method.Name}.");
        }
    }

    /// <summary>Dispatches the Redis interfaces to the bounded offline command implementation.</summary>
    public class RedisProxy : DispatchProxy
    {
        /// <summary>Fixture owning the synthetic records.</summary>
        public RedisLearnerTestData Fixture { get; set; } = null!;

        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Fixture.Invoke(targetMethod!, args!);
    }
}
