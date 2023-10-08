using Microsoft.EntityFrameworkCore;

namespace ChocolateStores.Context;

internal class InStoreCacheKey
{
    private readonly Type _dbContextType;
    private readonly bool _designTime;
    private readonly string? _schema;

    public InStoreCacheKey(DbContext context, bool designTime)
    {
        _dbContextType = context.GetType();
        _designTime = designTime;
        _schema = (context as IInStoreContext)?.Schema;
    }

    protected bool Equals(InStoreCacheKey other) =>
        _dbContextType == other._dbContextType
        && _designTime == other._designTime
        && _schema == other._schema;

    public override bool Equals(object? obj) =>
        (obj is InStoreCacheKey otherAsKey) && Equals(otherAsKey);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(_dbContextType);
        hash.Add(_designTime);
        hash.Add(_schema);

        return hash.ToHashCode();
    }
}
