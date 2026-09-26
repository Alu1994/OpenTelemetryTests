FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ApiOTEL/ApiOTEL.csproj", "src/ApiOTEL/"]
COPY ["aspire/ApiOTEL.ServiceDefaults/ApiOTEL.ServiceDefaults.csproj", "aspire/ApiOTEL.ServiceDefaults/"]
RUN dotnet restore "src/ApiOTEL/ApiOTEL.csproj"
COPY . .
WORKDIR "/src/src/ApiOTEL"
RUN dotnet build "./ApiOTEL.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ApiOTEL.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiOTEL.dll"]
