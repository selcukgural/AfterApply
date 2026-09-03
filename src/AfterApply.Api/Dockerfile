# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/AfterApply.Domain/AfterApply.Domain.csproj src/AfterApply.Domain/
COPY src/AfterApply.Application/AfterApply.Application.csproj src/AfterApply.Application/
COPY src/AfterApply.Infrastructure/AfterApply.Infrastructure.csproj src/AfterApply.Infrastructure/
COPY src/AfterApply.Api/AfterApply.Api.csproj src/AfterApply.Api/
RUN dotnet restore src/AfterApply.Api/AfterApply.Api.csproj
COPY src/ src/
RUN dotnet publish src/AfterApply.Api/AfterApply.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
# The base image ships a non-root "app" user but still defaults to root; opting in costs nothing
# here. Nothing in the container needs root: the only thing this process writes is the staged
# import file under /tmp (ImportService.StageFileAsync), and 8080 is above the privileged range.
USER app
ENTRYPOINT ["dotnet", "AfterApply.Api.dll"]
