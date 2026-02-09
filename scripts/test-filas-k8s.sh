#!/usr/bin/env bash
set -euo pipefail

IDENTITY_URL="${IDENTITY_URL:-http://localhost:30081}"
PROPERTY_URL="${PROPERTY_URL:-http://localhost:30082}"
DATAINGESTION_URL="${DATAINGESTION_URL:-http://localhost:30080}"
API_KEY="${API_KEY:-dev-api-key-12345}"
TEST_EMAIL="${TEST_EMAIL:-admin@localhost}"
TEST_SENHA="${TEST_SENHA:-Admin@123}"

echo "================================================"
echo "  Teste de filas K8s (Identity -> Property -> DataIngestion -> Medicoes)"
echo "================================================"
echo "  Identity:       $IDENTITY_URL"
echo "  Property:       $PROPERTY_URL"
echo "  DataIngestion:  $DATAINGESTION_URL"
echo "  Usuário:        $TEST_EMAIL (outro: TEST_EMAIL=... TEST_SENHA=... $0)"
echo ""

curl_() {
  curl -s -w "\n%{http_code}" "$@"
}

id_from_json() {
  echo "$1" | grep -oE '"id"\s*:\s*"[0-9a-fA-F-]{36}"' | head -1 | grep -oE '[0-9a-fA-F-]{36}'
}

echo "1. Criar usuário (Identity) -> publica ProdutorDataMessage -> Medicoes consome"
RES=$(curl_ -X POST "$IDENTITY_URL/api/users" \
  -H "Content-Type: application/json" \
  -d "{\"nome\":\"Teste Filas\",\"email\":\"$TEST_EMAIL\",\"senha\":\"$TEST_SENHA\"}")
HTTP=$(echo "$RES" | tail -n1)
BODY=$(echo "$RES" | sed '$d')
if [[ "$HTTP" == "201" ]]; then
  echo "   OK (201). User criado."
  PRODUTOR_ID=$(id_from_json "$BODY")
  echo "   ProdutorId: $PRODUTOR_ID"
else
  [[ "$HTTP" == "400" ]] && echo "   Usuário já existe; seguindo para login."
fi

echo ""
echo "2. Login (Identity) para obter JWT"
RES=$(curl_ -X POST "$IDENTITY_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$TEST_EMAIL\",\"senha\":\"$TEST_SENHA\"}")
HTTP=$(echo "$RES" | tail -n1)
BODY=$(echo "$RES" | sed '$d')
if [[ "$HTTP" != "200" ]]; then
  echo "   Falhou: HTTP $HTTP. Verifique credenciais ou defina TOKEN=seu_jwt para pular login."
  if [[ -z "${TOKEN:-}" ]]; then
    exit 1
  fi
else
  TOKEN=$(echo "$BODY" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
  echo "   OK. JWT obtido."
fi

echo ""
echo "3. Criar propriedade (Property) -> publica PropriedadeDataMessage -> Medicoes consome"
RES=$(curl_ -X POST "$PROPERTY_URL/api/propriedades" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${TOKEN:-}" \
  -d '{"nome":"Prop Teste Filas","descricao":"Teste"}')
HTTP=$(echo "$RES" | tail -n1)
BODY=$(echo "$RES" | sed '$d')
if [[ "$HTTP" == "201" ]]; then
  PROP_ID=$(id_from_json "$BODY")
  echo "   OK (201). PropriedadeId: $PROP_ID"
else
  echo "   Falhou: HTTP $HTTP - $BODY"
  PROP_ID=""
fi

echo ""
echo "4. Criar talhão (Property) -> publica TalhaoDataMessage -> Medicoes consome"
if [[ -n "${PROP_ID:-}" ]]; then
  RES=$(curl_ -X POST "$PROPERTY_URL/api/talhoes/propriedade/$PROP_ID" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer ${TOKEN:-}" \
    -d '{"nome":"Talhao Teste","cultura":"Soja","descricao":null,"areaHectares":10}')
  HTTP=$(echo "$RES" | tail -n1)
  BODY=$(echo "$RES" | sed '$d')
  if [[ "$HTTP" == "201" ]]; then
    TALHAO_ID=$(id_from_json "$BODY")
    echo "   OK (201). TalhaoId: $TALHAO_ID"
  else
    echo "   Falhou: HTTP $HTTP - $BODY"
    TALHAO_ID=""
  fi
else
  TALHAO_ID=""
fi

echo ""
echo "5. Ingerir dado de sensor (DataIngestion) -> publica SensorDataMessage -> Medicoes consome"
if [[ -n "${TALHAO_ID:-}" ]]; then
  DATA_MEDICAO=$(date -u +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null || date -u +"%Y-%m-%dT%H:%M:%S.000Z")
  RES=$(curl_ -X POST "$DATAINGESTION_URL/api/sensordata" \
    -H "Content-Type: application/json" \
    -H "X-API-Key: $API_KEY" \
    -d "{\"talhaoId\":\"$TALHAO_ID\",\"dataMedicao\":\"$DATA_MEDICAO\",\"tipo\":0,\"valor\":25.5}")
  HTTP=$(echo "$RES" | tail -n1)
  BODY=$(echo "$RES" | sed '$d')
  if [[ "$HTTP" == "202" ]]; then
    echo "   OK (202). Dado enviado para a fila."
  else
    echo "   Falhou: HTTP $HTTP - $BODY"
  fi
else
  echo "   Pulado (sem TalhaoId)."
fi

echo ""
echo "================================================"
echo "  Verificar consumo no Medicoes (worker)"
echo "================================================"
echo "  kubectl logs deployment/agro-medicoes-worker -n agro-medicoes --tail=30"
echo "  Procure por: 'Mensagem recebida na fila de' (produtores, Propriedade, talhões, sensores)"
echo ""
