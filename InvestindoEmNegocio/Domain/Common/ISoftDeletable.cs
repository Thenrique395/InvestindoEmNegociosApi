namespace InvestindoEmNegocio.Domain.Common;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
}
