FROM mcr.microsoft.com/dotnet/sdk:6.0 as build

COPY ./src/ /src

RUN dotnet publish --configuration Release \
                   --runtime linux-musl-x64 \
                   --self-contained \
                   --output /out \
                   /src/Westermo.Nuget.ReadThroughCache/Westermo.Nuget.ReadThroughCache.csproj

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

COPY --from=build /out /app

# Default to "simple console logger" (not sure I want JSON...)
ENV Logging__Console__FormatterName=""

VOLUME /cache-root

WORKDIR /app

ENTRYPOINT ["/app/Westermo.Nuget.ReadThroughCache"]