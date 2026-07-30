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

        // Identificação
        builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.InstitutionName).HasMaxLength(160);
        builder.Property(x => x.ContractType).HasConversion<string>().HasMaxLength(40).IsRequired();

        // Valores
        builder.Property(x => x.PrincipalAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.AssetAmount).HasColumnType("numeric(14,2)");
        builder.Property(x => x.DownPaymentAmount).HasColumnType("numeric(14,2)");
        builder.Property(x => x.FinancedAmount).HasColumnType("numeric(14,2)").IsRequired();

        // Taxas
        builder.Property(x => x.AnnualInterestRate).HasColumnType("numeric(7,4)").IsRequired();
        builder.Property(x => x.MonthlyInterestRate).HasColumnType("numeric(9,6)").IsRequired();
        builder.Property(x => x.InterestRatePeriod).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.EffectiveAnnualRate).HasColumnType("numeric(9,6)");
        builder.Property(x => x.CetRate).HasColumnType("numeric(9,6)");

        // Prazo
        builder.Property(x => x.TermMonths).IsRequired();
        builder.Property(x => x.OriginalTermMonths).IsRequired();
        builder.Property(x => x.GracePeriodMonths).IsRequired();
        builder.Property(x => x.AmortizationType).HasConversion<string>().IsRequired();

        // Resultados do cronograma
        builder.Property(x => x.MonthlyPayment).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.TotalCost).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.TotalInterest).HasColumnType("numeric(14,2)").IsRequired();

        // Acompanhamento
        builder.Property(x => x.OpenBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PaidAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PaidPrincipal).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PaidInterest).HasColumnType("numeric(14,2)").IsRequired();

        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Concorrência otimista portável (Postgres + SQLite nos testes), mesmo padrão de LoanInstallment.
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.Status });
        builder.HasIndex(x => new { x.SpaceId, x.Status });
    }
}
