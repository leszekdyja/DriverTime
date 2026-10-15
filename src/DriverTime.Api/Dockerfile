FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DriverTime.sln ./
COPY src/DriverTime.Api/DriverTime.Api.csproj src/DriverTime.Api/
COPY src/DriverTime.Application/DriverTime.Application.csproj src/DriverTime.Application/
COPY src/DriverTime.Domain/DriverTime.Domain.csproj src/DriverTime.Domain/
COPY src/DriverTime.Infrastructure/DriverTime.Infrastructure.csproj src/DriverTime.Infrastructure/
RUN dotnet restore src/DriverTime.Api/DriverTime.Api.csproj

COPY src/DriverTime.Api/ src/DriverTime.Api/
COPY src/DriverTime.Application/ src/DriverTime.Application/
COPY src/DriverTime.Domain/ src/DriverTime.Domain/
COPY src/DriverTime.Infrastructure/ src/DriverTime.Infrastructure/
RUN dotnet publish src/DriverTime.Api/DriverTime.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl git python3 python3-tk python3-cryptography \
    && rm -rf /var/lib/apt/lists/*

ARG DDD_TACHOGRAPH_READER_COMMIT=ed033e5cc196ff9af252f6d328975602a48e01da
RUN git clone https://github.com/Syax89/DDDTachograph_Reader.git /opt/ddd-tachograph-reader \
    && git -C /opt/ddd-tachograph-reader checkout "$DDD_TACHOGRAPH_READER_COMMIT" \
    && rm -rf /opt/ddd-tachograph-reader/.git

WORKDIR /app
COPY --from=build /app/publish ./
COPY src/parser/ /app/parser/

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PYTHONUNBUFFERED=1 \
    PYTHONUTF8=1 \
    DDDTACHO_PARSER_ROOT=/opt/ddd-tachograph-reader

EXPOSE 8080
ENTRYPOINT ["dotnet", "DriverTime.Api.dll"]
