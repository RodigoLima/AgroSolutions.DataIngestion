#!/bin/bash

# Script para build e deploy da aplicação no Kind
set -e

echo "================================================"
echo "  Sensor Data Ingestion - Build & Deploy"
echo "================================================"
echo ""

# Cores para output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# ========= PATHS =========
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Função para print colorido
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 1. Verificar pré-requisitos
print_info "Verificando pré-requisitos..."

if ! command -v kind &> /dev/null; then
    print_error "Kind não está instalado."
    exit 1
fi

if ! command -v kubectl &> /dev/null; then
    print_error "kubectl não está instalado."
    exit 1
fi

if ! command -v docker &> /dev/null; then
    print_error "Docker não está instalado"
    exit 1
fi

print_info "Pré-requisitos OK"
echo ""

# 2. Criar cluster Kind (se não existir)
print_info "Verificando cluster Kind..."

if kind get clusters | grep -q "agro-dev"; then
    print_info "Cluster 'agro-dev' já existe."
else
    print_info "Criando cluster Kind agro-dev..."
    kind create cluster --config "$ROOT_DIR"/k8s/kind/config.yaml
fi

CURRENT_CONTEXT="$(kubectl config current-context)"
if [[ "$CURRENT_CONTEXT" != kind-* ]]; then
  print_error "Contexto atual ($CURRENT_CONTEXT) não é um cluster Kind"
  exit 1
fi

echo ""

if [ -z "${SKIP_BUILD:-}" ]; then
  print_info "Building imagem Docker..."
  docker build -t sensor-ingestion-api:latest "$ROOT_DIR"
  print_info "Carregando imagem no Kind..."
  kind load docker-image sensor-ingestion-api:latest --name agro-dev
  echo ""
fi

# 4. Deploy dos manifestos Kubernetes
print_info "Aplicando manifestos Kubernetes..."
cd "$ROOT_DIR/k8s"

kubectl apply -f "$ROOT_DIR"/k8s/namespaces.yaml
kubectl apply -f "$ROOT_DIR"/k8s/app/configmap.yaml
kubectl apply -f "$ROOT_DIR"/k8s/secrets.yaml
print_info "Deployando infraestrutura..."
kubectl apply -f "$ROOT_DIR"/k8s/infra/rabbitmq
kubectl apply -f "$ROOT_DIR"/k8s/infra/tempo
kubectl apply -f "$ROOT_DIR"/k8s/infra/loki
kubectl apply -f "$ROOT_DIR"/k8s/infra/prometheus
kubectl apply -f "$ROOT_DIR"/k8s/infra/collector
kubectl apply -f "$ROOT_DIR"/k8s/infra/grafana
WAIT_TO="${WAIT_TIMEOUT:-45}"
if kubectl wait --for=condition=ready pod -l app=rabbitmq -n sensor-ingestion --timeout=0s 2>/dev/null; then print_info "RabbitMQ já pronto."; else print_info "Aguardando RabbitMQ..."; kubectl wait --for=condition=ready pod -l app=rabbitmq -n sensor-ingestion --timeout="${WAIT_TO}s" 2>/dev/null || sleep 10; fi
print_info "Deployando aplicação..."
kubectl apply -f "$ROOT_DIR"/k8s/app

echo ""

# 5. Aguardar pods (pula se já prontos)
print_info "Verificando pods..."
for app in prometheus grafana sensor-api; do
  if kubectl wait --for=condition=ready pod -l app=$app -n sensor-ingestion --timeout=0s 2>/dev/null; then :; else kubectl wait --for=condition=ready pod -l app=$app -n sensor-ingestion --timeout="${WAIT_TO}s" 2>/dev/null || true; fi
done

echo ""

# 6. Verificar status
print_info "Status dos pods:"
kubectl get pods -n sensor-ingestion

echo ""
print_info "Serviços disponíveis:"
kubectl get svc -n sensor-ingestion

echo ""
echo "================================================"
echo "  ✅ Deploy Concluído com Sucesso!"
echo "================================================"
echo ""
echo "URLs de Acesso:"
echo "  🌐 API:             http://localhost:5000"
echo "  📊 Grafana:         http://localhost:30300 (admin/admin)"
echo "  📈 Prometheus:      http://localhost:30900"
echo "  🐰 RabbitMQ:        http://localhost:15672 (admin/admin123)"
echo ""
echo "Comandos úteis:"
echo "  kubectl get pods -n sensor-ingestion"
echo "  kubectl logs -f deployment/sensor-api -n sensor-ingestion"
echo "  kubectl delete namespace sensor-ingestion"
echo ""