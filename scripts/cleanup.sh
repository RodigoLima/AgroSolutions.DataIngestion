#!/bin/bash

# Script para limpar recursos do Kind
set -e

echo "================================================"
echo "  Limpando recursos do Kind"
echo "================================================"
echo ""

# Deletar namespace (remove todos os recursos)
echo "Deletando namespace sensor-ingestion..."
kubectl delete namespace sensor-ingestion --ignore-not-found=true

echo ""
echo "Excluíndo Cluster Kind..."

kind delete cluster --name agro-dev
echo "✅ Cluster deletado"

echo ""
echo "✅ Limpeza concluída"