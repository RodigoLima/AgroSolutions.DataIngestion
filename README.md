# AgroSolutions.DataIngestion

Microsserviço de ingestão de dados de sensores da plataforma AgroSolutions. Expõe API para receber telemetria (umidade do solo, temperatura, precipitação) por talhão e envia os dados para processamento via RabbitMQ. Autenticação via API Key (header `X-API-Key`).

## Tecnologias

- .NET 9
- MassTransit (RabbitMQ)
- OpenTelemetry (métricas, traces, logs)
- Docker e Kubernetes (Kind)
- Prometheus e Grafana

## Estrutura

```
src/
├── AgroSolutions.DataIngestion.Api
├── AgroSolutions.DataIngestion.Application
└── AgroSolutions.DataIngestion.Domain
tests/
├── AgroSolutions.DataIngestion.Application.Tests
└── AgroSolutions.DataIngestion.Domain.Tests
k8s/
├── kind/
├── app/
└── infra/   (rabbitmq, prometheus, grafana, etc.)
```

## Pré-requisitos

- .NET 9 SDK
- RabbitMQ

## Configuração

`appsettings.json` / variáveis de ambiente:

- `ApiKey:Key` – valor da API Key para autenticação
- `RabbitMQ:Host`, `Port`, `Username`, `Password`, `VirtualHost`

## Executar

```bash
dotnet restore AgroSolutions.DataIngestion.sln
dotnet run --project src/AgroSolutions.DataIngestion.Api
```

Swagger: conforme `ASPNETCORE_URLS` (ex.: `http://localhost:5000/swagger`).

## Endpoints principais

- `GET /health` – Health check (público)
- `GET /api/sensordata/status` – Status do serviço (público)
- `POST /api/sensordata` – Ingerir um dado de sensor (header `X-API-Key` obrigatório). Body: `TalhaoId`, `DataMedicao`, `Tipo` (Temperatura | Umidade | Precipitacao), `Valor`
- `POST /api/sensordata/batch` – Ingerir múltiplos dados (header `X-API-Key` obrigatório)

Os dados são publicados na fila para consumo pelo Worker de medições (AgroSolutions.Medicoes).

## Observabilidade

- Métricas Prometheus (`/metrics`), OpenTelemetry OTLP
- Dashboards Grafana em `k8s/infra/grafana`

## Testes e CI/CD

```bash
dotnet test AgroSolutions.DataIngestion.sln --configuration Release
```

Pipeline GitHub Actions: CI (build + testes) e CD (build e push da imagem Docker para Docker Hub).
