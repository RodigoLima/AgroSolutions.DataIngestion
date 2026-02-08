#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_INGESTION_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECTS_ROOT="$(cd "$DATA_INGESTION_ROOT/.." && pwd)"

DATA_INGESTION="$DATA_INGESTION_ROOT"
IDENTITY="$PROJECTS_ROOT/IdentityService"
PROPERTY="$PROJECTS_ROOT/PropertyService"
MEDICOES="$PROJECTS_ROOT/AgroSolutions.Medicoes"

case "${1:-}" in
  --no-build) export SKIP_BUILD=1; export WAIT_TIMEOUT=15; shift ;;
esac

echo "================================================"
echo "  Deploy Master - Todos os microserviços (Kind)"
echo "================================================"
[ -n "${SKIP_BUILD:-}" ] && echo "  (modo --no-build: só K8s, sem build; waits curtos)"
echo ""

command -v kind >/dev/null 2>&1 || { echo "Kind não instalado."; exit 1; }
command -v kubectl >/dev/null 2>&1 || { echo "kubectl não instalado."; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "Docker não instalado."; exit 1; }

run() {
  local dir="$1"
  local script="$2"
  local name="$3"
  echo ""
  echo ">>> $name"
  echo "----------------------------------------"
  if [ -f "$dir/$script" ]; then
    (cd "$dir" && bash "$script")
  else
    echo "Script não encontrado: $dir/$script"
    exit 1
  fi
}

run "$DATA_INGESTION" "scripts/deploy.sh" "1/4 AgroSolutions.DataIngestion (infra + RabbitMQ)"
run "$IDENTITY" "scripts/deploy.sh" "2/4 IdentityService"
run "$PROPERTY" "scripts/deploy.sh" "3/4 PropertyService"
run "$MEDICOES" "scripts/deploy.sh" "4/4 AgroSolutions.Medicoes"

echo ""
echo "================================================"
echo "  Deploy Master concluído"
echo "================================================"
echo ""
echo "APIs:"
echo "  DataIngestion:  http://localhost:5000"
echo "  Identity:       http://localhost:30081"
echo "  Property:       http://localhost:30082"
echo "  Medicoes:       namespace agro-medicoes (worker)"
echo ""
echo "Infra:"
echo "  RabbitMQ:       http://localhost:15672 (admin/admin123)"
echo "  Grafana:        http://localhost:30300 | 30380 | 30381 (admin/admin)"
echo "  Prometheus:     http://localhost:30900 | 30980 | 30981"
echo ""
