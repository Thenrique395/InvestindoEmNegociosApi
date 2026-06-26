using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class UploadReceiptRequest
{
    public IFormFile? Receipt { get; init; }
}
