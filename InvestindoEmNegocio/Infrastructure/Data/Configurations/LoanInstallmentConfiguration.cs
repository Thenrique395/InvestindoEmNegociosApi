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
        builder.Property(x => x.InsuranceAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.FeeAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PenaltyAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.EndingBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PaidAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.RemainingAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.ScheduleVersion).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasIndex(x => new { x.UserId, x.DueDate });
        builder.HasIndex(x => new { x.ContractId, x.InstallmentNo }).IsUnique();

        // Modela a relação com o contrato (espelha a FK ON DELETE CASCADE do schema.sql).
        // Sem isso, o EF não conhece a dependência e a ordem de deleção não é garantida:
        // ao excluir um contrato, a cascata do banco e o delete explícito das parcelas
        // competem, gerando DbUpdateConcurrencyException (parcelas têm Version). Com a
        // relação modelada, basta excluir o contrato — a cascata cuida das parcelas.
        builder.HasOne<LoanContract>()
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
