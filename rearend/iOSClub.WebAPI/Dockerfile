FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["iOSClub.WebAPI/iOSClub.WebAPI.csproj", "iOSClub.WebAPI/"]
RUN dotnet restore "iOSClub.WebAPI/iOSClub.WebAPI.csproj"
COPY . .
WORKDIR "/src/iOSClub.WebAPI"
RUN dotnet build "./iOSClub.WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./iOSClub.WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/data /app/logs /app/keys \
    && chown -R $APP_UID:$APP_UID /app/data /app/logs /app/keys
USER $APP_UID
ENTRYPOINT ["dotnet", "iOSClub.WebAPI.dll"]
