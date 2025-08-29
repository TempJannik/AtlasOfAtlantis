FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for restore
COPY ["DOAMapper/DOAMapper.csproj", "DOAMapper/"]
COPY ["DOAMapper.Shared/DOAMapper.Shared.csproj", "DOAMapper.Shared/"]

# Restore dependencies
RUN dotnet restore "DOAMapper/DOAMapper.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/DOAMapper"
RUN dotnet build "./DOAMapper.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DOAMapper.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Create import directory
RUN mkdir -p /app/Data/Imports

# Copy published application
COPY --from=publish /app/publish .

# Set environment variable for Railway
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DOAMapper.dll"]
