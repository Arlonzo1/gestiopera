FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
RUN apk add --no-cache tzdata krb5-libs

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY BillaresPos.slnx ./
COPY src/BillaresPos.Domain/BillaresPos.Domain.csproj src/BillaresPos.Domain/
COPY src/BillaresPos.Application/BillaresPos.Application.csproj src/BillaresPos.Application/
COPY src/BillaresPos.Infrastructure/BillaresPos.Infrastructure.csproj src/BillaresPos.Infrastructure/
COPY src/BillaresPos.Api/BillaresPos.Api.csproj src/BillaresPos.Api/
RUN dotnet restore src/BillaresPos.Api/BillaresPos.Api.csproj
COPY . .
RUN dotnet publish src/BillaresPos.Api/BillaresPos.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BillaresPos.Api.dll"]


