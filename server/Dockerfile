# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy every project file first (better Docker layer caching restore only reruns
# if a .csproj actually changed, not on every source edit)
COPY Pos.Domain/*.csproj Pos.Domain/
COPY Pos.Application/*.csproj Pos.Application/
COPY Pos.Infrastructure/*.csproj Pos.Infrastructure/
COPY Pos.API/*.csproj Pos.API/

RUN dotnet restore Pos.API/Pos.API.csproj

# Now copy everything else and build
COPY . .
RUN dotnet publish Pos.API/Pos.API.csproj -c Release -o /app/publish --no-restore

# Runtime stage — much smaller image, no SDK needed to just run the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway assigns a dynamic port via $PORT — the app must listen on whatever that is,
# not a hardcoded port. This is set here as a fallback; Program.cs also reads $PORT
# directly (see PORT-BINDING.md), which is the part that actually matters at runtime.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Pos.API.dll"]
