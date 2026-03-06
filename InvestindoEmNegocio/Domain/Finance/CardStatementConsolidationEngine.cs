namespace InvestindoEmNegocio.Domain.Finance;

public static class CardStatementConsolidationEngine
{
    public static decimal NormalizeOpenAmount(decimal installmentAmount, decimal paidAmount)
    {
        var open = installmentAmount - paidAmount;
        return open <= 0 ? 0 : open;
    }
}
