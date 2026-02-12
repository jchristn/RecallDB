FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY src/RecallDb.Core/RecallDb.Core.csproj RecallDb.Core/
COPY src/RecallDb.Server/RecallDb.Server.csproj RecallDb.Server/
RUN dotnet restore RecallDb.Server/RecallDb.Server.csproj -a $TARGETARCH
COPY src/RecallDb.Core/ RecallDb.Core/
COPY src/RecallDb.Server/ RecallDb.Server/
WORKDIR /src/RecallDb.Server
RUN dotnet build -c Release -o /app/build -a $TARGETARCH
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false -a $TARGETARCH

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN for i in 1 2 3; do apt-get update && apt-get install -y --no-install-recommends \
    iputils-ping traceroute wget curl dnsutils net-tools netcat-openbsd vim iproute2 procps \
    && break || { sleep 5; rm -rf /var/lib/apt/lists/*; }; done
RUN rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8600
ENTRYPOINT ["dotnet", "RecallDb.Server.dll"]
