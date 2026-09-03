using System.Reflection;
using ECSPros.Api.Services.LegacyStock;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class LegacyStockMappingRepairPricingTests
{
    [TestMethod]
    public void ParentFiyatiVeMaliyeti_AynenKopyalanir()
    {
        var pricing = ValidateParentPricing(129.90m, 74.25m);

        Assert.AreEqual(129.90m, pricing.BasePrice);
        Assert.AreEqual(74.25m, pricing.BaseCost);
    }

    [TestMethod]
    public void ParentMaliyetiYoksa_NullKorunur()
    {
        var pricing = ValidateParentPricing(129.90m, null);

        Assert.AreEqual(129.90m, pricing.BasePrice);
        Assert.IsNull(pricing.BaseCost);
    }

    [TestMethod]
    public void PozitifOlmayanParentFiyati_FailClosedReddedilir()
    {
        foreach (var basePrice in new[] { 0m, -0.01m })
        {
            var wrapper = Assert.ThrowsExactly<TargetInvocationException>(
                () => ValidateParentPricing(basePrice, 10m));

            Assert.IsInstanceOfType<InvalidOperationException>(wrapper.InnerException);
            StringAssert.Contains(wrapper.InnerException.Message, "pozitif değil");
        }
    }

    private static (decimal BasePrice, decimal? BaseCost) ValidateParentPricing(
        decimal basePrice, decimal? baseCost)
    {
        var method = typeof(LegacyStockMappingRepairService).GetMethod(
            "ValidateParentPricing", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Parent fiyat doğrulama sözleşmesi bulunamadı.");

        return ((decimal BasePrice, decimal? BaseCost))method.Invoke(
            null, ["test-barcode", "test-product", basePrice, baseCost])!;
    }
}
