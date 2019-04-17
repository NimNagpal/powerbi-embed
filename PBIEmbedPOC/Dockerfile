FROM mcr.microsoft.com/dotnet/core/aspnet:2.2-stretch-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS build
WORKDIR /src
COPY ["PowerBIEmbedPOC/PowerBIEmbedPOC.csproj", "PowerBIEmbedPOC/"]
RUN dotnet restore "PowerBIEmbedPOC/PowerBIEmbedPOC.csproj"
COPY . .
WORKDIR "/src/PowerBIEmbedPOC"
RUN dotnet build "PowerBIEmbedPOC.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "PowerBIEmbedPOC.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "PowerBIEmbedPOC.dll"]