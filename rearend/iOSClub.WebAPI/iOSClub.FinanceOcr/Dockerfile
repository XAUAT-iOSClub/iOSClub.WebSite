FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
WORKDIR /src
COPY iOSClub.FinanceOcr.csproj .
RUN dotnet restore -r linux-x64
COPY Program.cs InvoiceRecognizer.cs ./
RUN dotnet publish -c Release -r linux-x64 --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble
RUN apt-get update && apt-get install -y --no-install-recommends curl poppler-utils poppler-data fonts-wqy-zenhei \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish/iOSClub.FinanceOcr .
ENV ASPNETCORE_URLS=http://+:5099
EXPOSE 5099
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=4 \
    CMD curl -fsS http://127.0.0.1:5099/health || exit 1
ENTRYPOINT ["./iOSClub.FinanceOcr"]
