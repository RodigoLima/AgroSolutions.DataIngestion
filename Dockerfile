# Estágio base para runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Estágio de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar arquivos de projeto
COPY ["src/AgroSolutions.DataIngestion.Api/AgroSolutions.DataIngestion.Api.csproj", "AgroSolutions.DataIngestion.Api/"]
COPY ["src/AgroSolutions.DataIngestion.Application/AgroSolutions.DataIngestion.Application.csproj", "AgroSolutions.DataIngestion.Application/"]
COPY ["src/AgroSolutions.DataIngestion.Domain/AgroSolutions.DataIngestion.Domain.csproj", "AgroSolutions.DataIngestion.Domain/"]

# Restore de dependências
RUN dotnet restore "AgroSolutions.DataIngestion.Api/AgroSolutions.DataIngestion.Api.csproj"

# Copiar todo o código fonte
COPY src/ .

# Build do projeto
WORKDIR "/src/AgroSolutions.DataIngestion.Api"
RUN dotnet build "AgroSolutions.DataIngestion.Api.csproj" -c Release -o /app/build

# Estágio de publish
FROM build AS publish
RUN dotnet publish "AgroSolutions.DataIngestion.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Criar usuário não-root
RUN addgroup --system --gid 1000 appuser \
    && adduser --system --uid 1000 --ingroup appuser --shell /bin/sh appuser

# Mudar ownership dos arquivos
RUN chown -R appuser:appuser /app

# Usar usuário não-root
USER appuser

ENTRYPOINT ["dotnet", "AgroSolutions.DataIngestion.Api.dll"]