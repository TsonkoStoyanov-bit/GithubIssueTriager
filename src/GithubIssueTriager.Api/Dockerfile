# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Run as root (not the template's $APP_UID): the Settings page writes back to
# appsettings.json on the Api host, which a non-root user can't do for the
# root-owned files copied into /app.
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/GithubIssueTriager.Api/GithubIssueTriager.Api.csproj", "src/GithubIssueTriager.Api/"]
COPY ["src/GithubIssueTriager.Shared/GithubIssueTriager.Shared.csproj", "src/GithubIssueTriager.Shared/"]
RUN dotnet restore "./src/GithubIssueTriager.Api/GithubIssueTriager.Api.csproj"
COPY . .
WORKDIR "/src/src/GithubIssueTriager.Api"
RUN dotnet build "./GithubIssueTriager.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GithubIssueTriager.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
# Runtime settings the original hand-written Dockerfile had and the VS template
# dropped: bind to 8080 (docker-compose maps it to host 5101), and flag the
# container so the app skips HTTPS redirection (no dev cert inside the container).
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GithubIssueTriager.Api.dll"]