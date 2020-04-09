FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["TheoremSlackBot.csproj", "./"]
RUN dotnet restore "./TheoremSlackBot.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "TheoremSlackBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TheoremSlackBot.csproj" -c Release -o /app/publish -p:PublishReadyToRun=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY "appsettings.json" .
ENTRYPOINT ["dotnet", "TheoremSlackBot.dll"]