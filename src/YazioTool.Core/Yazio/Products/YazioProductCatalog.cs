using YazioTool.Core.Yazio.Api;

namespace YazioTool.Core.Yazio.Products;

internal sealed class YazioProductCatalog(IYazioApiClient api) : IProductCatalog
{
    public async Task<ProductInfo?> FindAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await api.GetProductAsync(productId, cancellationToken);
        if (product is null)
            return null;

        var name = string.IsNullOrWhiteSpace(product.Producer) ? product.Name : $"{product.Name} ({product.Producer})";
        return new ProductInfo(productId, name, product.BaseUnit ?? "g", YazioFormats.ToNutrition(product.Nutrients));
    }
}
