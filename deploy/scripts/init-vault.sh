#!/usr/bin/env bash
set -euo pipefail

NAMESPACE="vault"
POD_NAME="vault-0"
VAULT_KEYS_FILE="../../vault-keys.json"

echo "Checking Vault status..."
INIT_STATUS=$(kubectl exec -n $NAMESPACE $POD_NAME -- vault status -format=json 2>/dev/null | grep '"initialized"' | grep -o 'true\|false' || echo "false")

if [[ "$INIT_STATUS" == "false" ]]; then
  echo "Initializing Vault..."
  kubectl exec -n $NAMESPACE $POD_NAME -- vault operator init -key-shares=1 -key-threshold=1 -format=json > "$VAULT_KEYS_FILE"
  
  UNSEAL_KEY=$(grep -o '"unseal_keys_b64": \[[^]]*\]' "$VAULT_KEYS_FILE" | grep -o '"[^"]*"' | head -1 | tr -d '"')
  ROOT_TOKEN=$(grep -o '"root_token": "[^"]*"' "$VAULT_KEYS_FILE" | grep -o '"[^"]*"' | tail -1 | tr -d '"')
  
  echo "Unsealing Vault..."
  kubectl exec -n $NAMESPACE $POD_NAME -- vault operator unseal "$UNSEAL_KEY"
  
  echo "Configuring Vault Kubernetes Auth..."
  kubectl exec -n $NAMESPACE $POD_NAME -- sh -c "
    export VAULT_TOKEN=$ROOT_TOKEN
    vault auth enable kubernetes
    vault write auth/kubernetes/config \
      kubernetes_host=https://\$KUBERNETES_PORT_443_TCP_ADDR:443
    
    vault policy write urfu-link-prod - <<EOF
path \"kv/*\" {
  capabilities = [\"read\", \"list\"]
}
EOF
    
    vault write auth/kubernetes/role/urfu-link-prod \
      bound_service_account_names=external-secrets \
      bound_service_account_namespaces=external-secrets \
      policies=urfu-link-prod \
      ttl=24h
      
    vault secrets enable -version=2 kv
  "
  echo "Vault initialized. Keys saved to $VAULT_KEYS_FILE (KEEP THIS SAFE!)"
else
  echo "Vault is already initialized."
  SEALED_STATUS=$(kubectl exec -n $NAMESPACE $POD_NAME -- vault status -format=json 2>/dev/null | grep '"sealed"' | grep -o 'true\|false' || echo "true")
  
  if [[ "$SEALED_STATUS" == "true" ]]; then
    if [[ -f "$VAULT_KEYS_FILE" ]]; then
      echo "Vault is sealed. Attempting to unseal using local keys file..."
      UNSEAL_KEY=$(grep -o '"unseal_keys_b64": \[[^]]*\]' "$VAULT_KEYS_FILE" | grep -o '"[^"]*"' | head -1 | tr -d '"')
      kubectl exec -n $NAMESPACE $POD_NAME -- vault operator unseal "$UNSEAL_KEY"
      echo "Vault unsealed successfully."
    else
      echo "WARNING: Vault is sealed and $VAULT_KEYS_FILE not found! You must unseal it manually."
    fi
  else
    echo "Vault is already unsealed."
  fi
fi
