# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/GithubIssueTriager.Shared/GithubIssueTriager.Shared.csproj src/GithubIssueTriager.Shared/
COPY src/GithubIssueTriager.Web/GithubIssueTriager.Web.csproj src/GithubIssueTriager.Web/
RUN dotnet restore src/GithubIssueTriager.Web/GithubIssueTriager.Web.csproj

COPY src/GithubIssueTriager.Shared/ src/GithubIssueTriager.Shared/
COPY src/GithubIssueTriager.Web/ src/GithubIssueTriager.Web/
RUN dotnet publish src/GithubIssueTriager.Web/GithubIssueTriager.Web.csproj -c Release -o /app --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "GithubIssueTriager.Web.dll"]
