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
echo "  Cluster: agro-dev"
[ -n "${SKIP_BUILD:-}" ] && echo "  Modo: --no-build (sem build de imagens)"
echo ""

command -v kind >/dev/null 2>&1 || { echo "Kind não instalado."; exit 1; }
command -v kubectl >/dev/null 2>&1 || { echo "kubectl não instalado."; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "Docker não instalado."; exit 1; }

for d in "$DATA_INGESTION" "$IDENTITY" "$PROPERTY" "$MEDICOES"; do
  [ -d "$d" ] || { echo "Diretório não encontrado: $d"; exit 1; }
done

if kind get clusters 2>/dev/null | grep -q "^agro-dev$"; then
  [ "$(kubectl config current-context 2>/dev/null)" != "kind-agro-dev" ] && kubectl config use-context kind-agro-dev 2>/dev/null || true
else
  echo "Cluster agro-dev não existe; será criado no 1º deploy."
fi

run() {
  local dir="$1" script="$2" name="$3"
  echo ""
  echo ">>> $name"
  echo "----------------------------------------"
  [ -f "$dir/$script" ] || { echo "Script não encontrado: $dir/$script"; exit 1; }
  (cd "$dir" && bash "$script")
}

run "$DATA_INGESTION" "scripts/deploy.sh" "1/4 DataIngestion (infra + RabbitMQ)"
run "$IDENTITY" "scripts/deploy.sh" "2/4 IdentityService"
run "$PROPERTY" "scripts/deploy.sh" "3/4 PropertyService"
run "$MEDICOES" "scripts/deploy.sh" "4/4 Medicoes"

echo ""
echo "================================================"
echo "  Deploy Master concluído"
echo "================================================"
echo ""
echo "APIs:"
echo "  DataIngestion:  http://localhost:30080/swagger"
echo "  Identity:       http://localhost:30081/swagger"
echo "  Property:       http://localhost:30082/swagger"
echo "  Medicoes:       namespace agro-medicoes (worker)"
echo ""
echo "Infra:"
echo "  RabbitMQ:       http://localhost:15672 (admin/admin123)"
echo "  Grafana:"
echo "    DataIngestion:  http://localhost:30300 (admin/admin)"
echo "    Identity:       http://localhost:30381 (admin/admin)"
echo "    Property:       http://localhost:30380 (admin/admin)"
echo "    Medicoes:       http://localhost:30000 (admin/admin)"
echo "  Prometheus:"
echo "    DataIngestion:  http://localhost:30900"
echo "    Identity:       http://localhost:30981"
echo "    Property:       http://localhost:30980"
echo "    Medicoes:       http://localhost:30902"
echo "  Mailpit:         http://localhost:30025 (UI)  |  SMTP: localhost:31025"
echo ""
echo "No Windows, se Prometheus/Grafana não abrirem: kubectl port-forward -n <namespace> svc/<service> <porta-local>:<porta-svc>"
echo ""
