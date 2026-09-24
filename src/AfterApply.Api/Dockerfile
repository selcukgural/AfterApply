# syntax=docker/dockerfile:1
# Base images are pinned by digest (tag kept for readability); Dependabot's docker updates move
# the digest forward.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/AfterApply.Domain/AfterApply.Domain.csproj src/AfterApply.Domain/
COPY src/AfterApply.Application/AfterApply.Application.csproj src/AfterApply.Application/
COPY src/AfterApply.Infrastructure/AfterApply.Infrastructure.csproj src/AfterApply.Infrastructure/
COPY src/AfterApply.Api/AfterApply.Api.csproj src/AfterApply.Api/
RUN dotnet restore src/AfterApply.Api/AfterApply.Api.csproj
COPY src/ src/
RUN dotnet publish src/AfterApply.Api/AfterApply.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
# The base image ships a non-root "app" user but still defaults to root; opting in costs nothing
# here. Nothing in the container needs root: the only thing this process writes is the staged
# import file under /tmp (ImportService.StageFileAsync), and 8080 is above the privileged range.
USER app
ENTRYPOINT ["dotnet", "AfterApply.Api.dll"]
