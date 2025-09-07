FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Theorem/Theorem.csproj", "./"]
RUN dotnet restore "./Theorem.csproj"
COPY src/Theorem/ .
WORKDIR "/src/."
RUN dotnet build "Theorem.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Theorem.csproj" -c Release -o /app/publish -p:PublishReadyToRun=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Theorem.dll"]
