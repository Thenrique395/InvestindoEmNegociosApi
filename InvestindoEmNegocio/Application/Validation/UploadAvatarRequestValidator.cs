using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UploadAvatarRequestValidator : AbstractValidator<UploadAvatarRequest>
{
    private static readonly HashSet<string> AllowedTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public UploadAvatarRequestValidator()
    {
        RuleFor(x => x.Avatar)
            .NotNull().WithMessage("Envie uma imagem válida.");

        RuleFor(x => x.Avatar!.Length)
            .GreaterThan(0).WithMessage("Envie uma imagem válida.")
            .LessThanOrEqualTo(2 * 1024 * 1024).WithMessage("Imagem deve ter no máximo 2 MB.")
            .When(x => x.Avatar is not null);

        RuleFor(x => x.Avatar!.ContentType)
            .Must(AllowedTypes.Contains)
            .WithMessage("Formato não suportado. Use PNG, JPG ou WEBP.")
            .When(x => x.Avatar is not null);
    }
}
