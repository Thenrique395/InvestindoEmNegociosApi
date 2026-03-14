using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class LoanContractConfiguration : IEntityTypeConfiguration<LoanContract>
{
    public void Configure(EntityTypeBuilder<LoanContract> builder)
    {
        builder.ToTable("loan_contracts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
        builder.Property(x => x.PrincipalAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.AnnualInterestRate).HasColumnType("numeric(7,4)").IsRequired();
        builder.Property(x => x.MonthlyPayment).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.TotalCost).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.TotalInterest).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.AmortizationType).HasConversion<string>().IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.Status });
    }
}
