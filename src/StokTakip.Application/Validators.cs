using FluentValidation;

namespace StokTakip.Application;

static class Rules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> r) =>
        r.NotEmpty().MinimumLength(10).MaximumLength(100)
         .Matches("[A-Z]").WithMessage("Parola en az bir büyük harf içermeli.")
         .Matches("[a-z]").WithMessage("Parola en az bir küçük harf içermeli.")
         .Matches("[0-9]").WithMessage("Parola en az bir rakam içermeli.")
         .Matches("[^a-zA-Z0-9]").WithMessage("Parola en az bir özel karakter içermeli.");
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator() { RuleFor(x => x.Username).NotEmpty().MaximumLength(50); RuleFor(x => x.Password).NotEmpty().MaximumLength(100); }
}
public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50).Matches("^[a-zA-Z0-9._-]+$");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.Role).IsInEnum();
    }
}
public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator() => RuleFor(x => x.NewPassword).StrongPassword();
}
public class CategoryCreateDtoValidator : AbstractValidator<CategoryCreateDto>
{
    public CategoryCreateDtoValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}
public class PartUpsertDtoValidator : AbstractValidator<PartUpsertDto>
{
    public PartUpsertDtoValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9._-]+$").WithMessage("Parça kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.");
        RuleFor(x => x.Barcode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IssuePolicy).IsInEnum();
    }
}
public class MoveStockDtoValidator : AbstractValidator<MoveStockDto>
{
    public MoveStockDtoValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Miktar pozitif olmalı; yön hareket tipinden belirlenir.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.LotNumber).MaximumLength(50);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x).Must(x => x.ProductionDate is null || x.ExpiryDate is null || x.ExpiryDate >= x.ProductionDate)
            .WithMessage("SKT/garanti tarihi üretim tarihinden önce olamaz.");
    }
}
public class TicketCreateDtoValidator : AbstractValidator<TicketCreateDto>
{
    public TicketCreateDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Category).IsInEnum();
    }
}
public class LeaveCreateDtoValidator : AbstractValidator<LeaveCreateDto>
{
    public LeaveCreateDtoValidator()
    {
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
