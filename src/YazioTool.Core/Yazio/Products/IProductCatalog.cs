using YazioTool.Core.Domain;

namespace YazioTool.Core.Yazio.Products;

internal sealed record ProductInfo(Guid Id, string Name, string BaseUnit, Nutrition PerBaseUnit);

internal interface IProductCatalog
{
    Task<ProductInfo?> FindAsync(Guid productId, CancellationToken cancellationToken);
}
