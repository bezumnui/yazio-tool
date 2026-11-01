using System.Collections.Concurrent;

namespace YazioTool.Core.Yazio.Products;

/// <summary>Decorator that keeps found products in memory: product data rarely changes.</summary>
internal sealed class CachingProductCatalog(IProductCatalog inner) : IProductCatalog
{
    private readonly ConcurrentDictionary<Guid, ProductInfo> _cache = new();

    public async Task<ProductInfo?> FindAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(productId, out var cached))
            return cached;

        var product = await inner.FindAsync(productId, cancellationToken);
        if (product is not null)
            _cache[productId] = product;

        return product;
    }
}
