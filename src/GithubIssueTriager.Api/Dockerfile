# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so NuGet restore is cached unless dependencies change.
COPY src/GithubIssueTriager.Shared/GithubIssueTriager.Shared.csproj src/GithubIssueTriager.Shared/
COPY src/GithubIssueTriager.Api/GithubIssueTriager.Api.csproj src/GithubIssueTriager.Api/
RUN dotnet restore src/GithubIssueTriager.Api/GithubIssueTriager.Api.csproj

# Copy the rest of the source and publish.
COPY src/GithubIssueTriager.Shared/ src/GithubIssueTriager.Shared/
COPY src/GithubIssueTriager.Api/ src/GithubIssueTriager.Api/
COPY fixtures/ fixtures/
RUN dotnet publish src/GithubIssueTriager.Api/GithubIssueTriager.Api.csproj -c Release -o /app --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "GithubIssueTriager.Api.dll"]
