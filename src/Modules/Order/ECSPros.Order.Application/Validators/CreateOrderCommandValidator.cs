using ECSPros.Order.Application.Commands.CreateOrder;
using FluentValidation;

namespace ECSPros.Order.Application.Validators;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.FirmPlatformId)
            .NotEmpty().WithMessage("Platform seçilmelidir.");

        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("Üye seçilmelidir.");

        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Para birimi seçilmelidir.")
            .Length(3).WithMessage("Para birimi kodu 3 karakter olmalıdır.");

        RuleFor(x => x.ShippingRecipientName)
            .NotEmpty().WithMessage("Alıcı adı boş olamaz.")
            .MaximumLength(150).WithMessage("Alıcı adı en fazla 150 karakter olabilir.");

        RuleFor(x => x.ShippingRecipientPhone)
            .NotEmpty().WithMessage("Alıcı telefon numarası boş olamaz.");

        RuleFor(x => x.ShippingAddressLine)
            .NotEmpty().WithMessage("Adres satırı boş olamaz.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Sipariş en az bir ürün içermelidir.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId)
                .NotEmpty().WithMessage("Varyant seçilmelidir.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Ürün miktarı sıfırdan büyük olmalıdır.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Birim fiyat negatif olamaz.");
        });
    }
}
