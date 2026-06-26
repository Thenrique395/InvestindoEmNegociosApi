using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Api;
using InvestindoEmNegocio.Infrastructure.Auth;
using InvestindoEmNegocio.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Net;

namespace InvestindoEmNegocio.Tests;

public class InfrastructureUtilityTests
{
    [Fact]
    public void JwtTokenGenerator_Should_Generate_Token_With_Expected_Claims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "issuer-test",
            Audience = "audience-test",
            SecretKey = "12345678901234567890123456789012",
            ExpiresMinutes = 30
        });

        var user = new User("Henrique", "henrique@test.com", "hash");
        user.SetRole(UserRole.Admin);

        var sut = new JwtTokenGenerator(options);

        var token = sut.Generate(user);

        token.Token.Should().NotBeNullOrWhiteSpace();
        token.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(29));
    }

    [Fact]
    public void JwtTokenGenerator_Should_Throw_When_Secret_Is_Missing()
    {
        var options = Options.Create(new JwtOptions { SecretKey = "" });
        var user = new User("Henrique", "henrique@test.com", "hash");
        var sut = new JwtTokenGenerator(options);

        Action act = () => sut.Generate(user);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task B3ApiClient_Should_Return_Null_When_Not_Available()
    {
        var httpClient = new HttpClient(new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var options = Options.Create(new B3ApiOptions { Enabled = false });
        var sut = new B3ApiClient(httpClient, options, NullLogger<B3ApiClient>.Instance);

        var result = await sut.GetLatestSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task B3ApiClient_Should_Deserialize_Snapshot_When_Response_Is_Valid()
    {
        var handler = new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "referenceMonth": "02/2026",
              "holderName": "JOAO",
              "document": "123",
              "positions": [],
              "incomes": [],
              "trades": []
            }
            """)
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://b3.test") };
        var options = Options.Create(new B3ApiOptions
        {
            Enabled = true,
            BaseUrl = "https://b3.test",
            ClientId = "client",
            ClientSecret = "secret"
        });

        var sut = new B3ApiClient(httpClient, options, NullLogger<B3ApiClient>.Instance);

        var result = await sut.GetLatestSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ReferenceMonth.Should().Be("02/2026");
        handler.LastRequest!.Headers.Contains("X-Client-Id").Should().BeTrue();
        handler.LastRequest.Headers.Contains("X-Client-Secret").Should().BeTrue();
    }

    [Fact]
    public void ListQueryHelper_Should_Apply_Sorting_And_Pagination_And_Write_Headers()
    {
        var data = new List<(string Name, int Value)>
        {
            ("B", 20),
            ("A", 10),
            ("C", 30)
        };

        var query = new ListQuery(Page: 1, PageSize: 2, SortBy: "value", SortDir: "desc");
        var result = ListQueryHelper.Apply(
            data,
            query,
            new Dictionary<string, Func<(string Name, int Value), object?>>
            {
                ["value"] = x => x.Value
            });

        result.Items.Should().HaveCount(2);
        result.Items[0].Value.Should().Be(30);
        result.Total.Should().Be(3);

        var response = new DefaultHttpContext().Response;
        ListQueryHelper.WritePaginationHeaders(response, result.Total, result.Page, result.PageSize);
        response.Headers["X-Total-Count"].ToString().Should().Be("3");
        response.Headers["X-Page"].ToString().Should().Be("1");
        response.Headers["X-Page-Size"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task AvatarStorageService_Should_Save_File_And_Return_Public_Url()
    {
        using var dir = new TempDir();
        var env = new MockWebHostEnvironment
        {
            ContentRootPath = dir.Path,
            WebRootPath = string.Empty
        };

        var sut = new AvatarStorageService(env);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        var url = await sut.SaveAsync(
            Guid.NewGuid(),
            stream,
            "avatar.png",
            "image/png",
            "https://cdn.test",
            CancellationToken.None);

        url.Should().StartWith("https://cdn.test/uploads/avatars/");
        Directory.Exists(System.IO.Path.Combine(dir.Path, "wwwroot", "uploads", "avatars")).Should().BeTrue();
    }

    [Fact]
    public async Task AvatarStorageService_Should_Reject_Unsupported_ContentType()
    {
        using var dir = new TempDir();
        var env = new MockWebHostEnvironment
        {
            ContentRootPath = dir.Path,
            WebRootPath = dir.Path
        };

        var sut = new AvatarStorageService(env);
        await using var stream = new MemoryStream([1, 2, 3]);

        Func<Task> act = async () => await sut.SaveAsync(
            Guid.NewGuid(),
            stream,
            "avatar.gif",
            "image/gif",
            "https://cdn.test",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ReceiptStorageService_Should_Save_File_And_Return_Public_Url()
    {
        using var dir = new TempDir();
        var env = new MockWebHostEnvironment
        {
            ContentRootPath = dir.Path,
            WebRootPath = string.Empty
        };

        var sut = new ReceiptStorageService(env);
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        var url = await sut.SaveAsync(
            Guid.NewGuid(),
            stream,
            "nota.pdf",
            "application/pdf",
            "https://cdn.test",
            CancellationToken.None);

        url.Should().StartWith("https://cdn.test/uploads/receipts/");
        Directory.Exists(System.IO.Path.Combine(dir.Path, "wwwroot", "uploads", "receipts")).Should().BeTrue();
    }

    [Fact]
    public async Task ReceiptStorageService_Should_Reject_Unsupported_ContentType()
    {
        using var dir = new TempDir();
        var env = new MockWebHostEnvironment
        {
            ContentRootPath = dir.Path,
            WebRootPath = dir.Path
        };

        var sut = new ReceiptStorageService(env);
        await using var stream = new MemoryStream([1, 2, 3]);

        Func<Task> act = async () => await sut.SaveAsync(
            Guid.NewGuid(),
            stream,
            "nota.docx",
            "application/msword",
            "https://cdn.test",
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private sealed class StaticHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class MockWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inv-negocio-inf-{Guid.NewGuid():N}");

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
