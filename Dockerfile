FROM gcc:4.9 AS gcc
COPY TribesVengeanceMasterServer/encrtypex_decoder.nix.c /app/encrtypex_decoder.nix.c
RUN gcc -shared "-fPIC" -o "/app/encrypex_decoder.so" /app/encrtypex_decoder.nix.c

FROM microsoft/dotnet:2.2-sdk-alpine AS build
WORKDIR /app
EXPOSE 27900
EXPOSE 28910

# copy csproj and restore as distinct layers
COPY TribesVengeanceMasterServer/*.csproj ./TribesVengeanceMasterServer/
WORKDIR /app/TribesVengeanceMasterServer
RUN dotnet restore

# copy and publish app and libraries
WORKDIR /app/
COPY TribesVengeanceMasterServer/. ./TribesVengeanceMasterServer/
WORKDIR /app/TribesVengeanceMasterServer
RUN dotnet publish -c Release -o out


# test application -- see: dotnet-docker-unit-testing.md
# FROM build AS testrunner
# WORKDIR /app/tests
# COPY tests/. .
# ENTRYPOINT ["dotnet", "test", "--logger:trx"]

FROM microsoft/dotnet:2.2-runtime-alpine AS runtime
WORKDIR /app
COPY --from=build /app/TribesVengeanceMasterServer/out ./
COPY --from=gcc /app/encrypex_decoder.so ./
ENTRYPOINT ["dotnet", "TribesVengeanceMasterServer.dll"]