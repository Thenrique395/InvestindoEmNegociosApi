FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./InvestindoEmNegocio/InvestindoEmNegocio.csproj
RUN dotnet publish ./InvestindoEmNegocio/InvestindoEmNegocio.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5059
COPY --from=build /app/publish .
EXPOSE 5059
ENTRYPOINT ["dotnet", "InvestindoEmNegocio.dll"]
