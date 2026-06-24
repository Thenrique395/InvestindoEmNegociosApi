using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class LoanInstallmentConfiguration : IEntityTypeConfiguration<LoanInstallment>
{
    public void Configure(EntityTypeBuilder<LoanInstallment> builder)
    {
        builder.ToTable("loan_installments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BeginningBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PrincipalAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.InterestAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.EndingBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasIndex(x => new { x.UserId, x.DueDate });
        builder.HasIndex(x => new { x.ContractId, x.InstallmentNo }).IsUnique();
    }
}
