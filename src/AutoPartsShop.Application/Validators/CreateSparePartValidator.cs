using AutoPartsShop.Application.DTOs.SpareParts;
using FluentValidation;

namespace AutoPartsShop.Application.Validators;

public class CreateSparePartValidator : AbstractValidator<CreateSparePartDto>
{
    public CreateSparePartValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم القطعة مطلوب.");

        RuleFor(x => x.PartNumber)
            .NotEmpty().WithMessage("رقم القطعة مطلوب.");

        RuleFor(x => x.Barcode)
            .NotEmpty().WithMessage("الباركود مطلوب.");

        // الأسعار والكميات تُدار عبر فواتير المشتريات - لا حاجة للتحقق منها هنا
    }
}
