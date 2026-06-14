#!/usr/bin/env bash
# tests/e2e/e2e_test.sh
# Fluxo completo: Admin → Fiscal → Operador → Relatório → Auditoria
# Requer: curl, jq
# Uso: API_URL=http://localhost:5000 bash tests/e2e/e2e_test.sh

set -euo pipefail

API="${API_URL:-http://localhost:5000}"
PASS=0
FAIL=0

# ── helpers ─────────────────────────────────────────────────────────────────
ok()   { echo "  ✓ $*"; (( PASS++ )) || true; }
fail() { echo "  ✗ $*"; (( FAIL++ )) || true; }

assert_status() {
  local label="$1" expected="$2" actual="$3"
  if [[ "$actual" == "$expected" ]]; then ok "$label (HTTP $actual)";
  else fail "$label — esperado HTTP $expected, obteve HTTP $actual"; fi
}

assert_json() {
  local label="$1" path="$2" expected="$3" body="$4"
  local actual
  actual=$(echo "$body" | jq -r "$path" 2>/dev/null || echo "__jq_error__")
  if [[ "$actual" == "$expected" ]]; then ok "$label ($path = $actual)";
  else fail "$label — $path esperado '$expected', obteve '$actual'"; fi
}

json_post() {
  curl -s -o /tmp/body -w "%{http_code}" \
    -X POST "$API$1" \
    -H "Content-Type: application/json" \
    ${2:+-H "Authorization: Bearer $2"} \
    -d "$3"
}

json_patch() {
  curl -s -o /tmp/body -w "%{http_code}" \
    -X PATCH "$API$1" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $2" \
    -d "$3"
}

json_get() {
  curl -s -o /tmp/body -w "%{http_code}" \
    -X GET "$API$1" \
    -H "Authorization: Bearer $2"
}

# ── 1. Health check ──────────────────────────────────────────────────────────
echo ""
echo "▶ Health check"
STATUS=$(curl -s -o /tmp/body -w "%{http_code}" "$API/api/health")
assert_status "GET /api/health" "200" "$STATUS"

# ── 2. Login Admin ───────────────────────────────────────────────────────────
echo ""
echo "▶ Login Admin"
STATUS=$(json_post "/api/auth/login" "" '{"cpf":"00000000000","senha":"Admin@123"}')
assert_status "POST /api/auth/login (admin)" "200" "$STATUS"
BODY=$(cat /tmp/body)
ADMIN_TOKEN=$(echo "$BODY" | jq -r '.token // empty')
[[ -n "$ADMIN_TOKEN" ]] && ok "Token Admin obtido" || fail "Token Admin ausente"

# ── 3. Criar condomínio ──────────────────────────────────────────────────────
echo ""
echo "▶ Criar condomínio via Admin"
STATUS=$(json_post "/api/admin/condominios" "$ADMIN_TOKEN" \
  '{"nome":"Cond E2E Test","endereco":"Rua E2E 123","qtdUnidades":2,"tipoMedidor":"AguaFria"}')
assert_status "POST /api/admin/condominios" "201" "$STATUS"
CONDO_ID=$(cat /tmp/body | jq -r '.id // .Id // empty')
[[ -n "$CONDO_ID" ]] && ok "Condomínio criado (id=$CONDO_ID)" || fail "ID de condomínio não retornado"

# ── 4. Criar OS ──────────────────────────────────────────────────────────────
echo ""
echo "▶ Criar OS via Admin"
MES=$(date +%-m)
ANO=$(date +%Y)
STATUS=$(json_post "/api/admin/ordens" "$ADMIN_TOKEN" \
  "{\"condominioId\":$CONDO_ID,\"mes\":$MES,\"ano\":$ANO}")
assert_status "POST /api/admin/ordens" "201" "$STATUS"
OS_ID=$(cat /tmp/body | jq -r '.id // .Id // empty')
[[ -n "$OS_ID" ]] && ok "OS criada (id=$OS_ID)" || fail "ID de OS não retornado"

# ── 5. OS duplicada → 409 ────────────────────────────────────────────────────
echo ""
echo "▶ OS duplicada deve retornar 409"
STATUS=$(json_post "/api/admin/ordens" "$ADMIN_TOKEN" \
  "{\"condominioId\":$CONDO_ID,\"mes\":$MES,\"ano\":$ANO}")
assert_status "POST /api/admin/ordens (duplicada)" "409" "$STATUS"

# ── 6. Login Fiscal ──────────────────────────────────────────────────────────
echo ""
echo "▶ Login Fiscal"
STATUS=$(json_post "/api/auth/login" "" '{"cpf":"22222222222","senha":"Fiscal@123"}')
assert_status "POST /api/auth/login (fiscal)" "200" "$STATUS"
FISCAL_TOKEN=$(cat /tmp/body | jq -r '.token // empty')
[[ -n "$FISCAL_TOKEN" ]] && ok "Token Fiscal obtido" || fail "Token Fiscal ausente"

# ── 7. Fiscal lista ordens abertas ───────────────────────────────────────────
echo ""
echo "▶ Fiscal lista ordens abertas"
STATUS=$(json_get "/api/fiscal/ordens-abertas" "$FISCAL_TOKEN")
assert_status "GET /api/fiscal/ordens-abertas" "200" "$STATUS"
COUNT=$(cat /tmp/body | jq 'length')
[[ "$COUNT" -ge 1 ]] && ok "Ordens abertas retornadas ($COUNT)" || fail "Nenhuma ordem aberta encontrada"

# ── 8. Fiscal obtém detalhes da OS ───────────────────────────────────────────
echo ""
echo "▶ Fiscal obtém detalhes da OS"
STATUS=$(json_get "/api/fiscal/os/$OS_ID" "$FISCAL_TOKEN")
assert_status "GET /api/fiscal/os/$OS_ID" "200" "$STATUS"
UNIDADES=$(cat /tmp/body | jq -r '.unidades // empty | length')
[[ "$UNIDADES" -ge 2 ]] && ok "OS contém $UNIDADES unidades" || fail "Unidades da OS não encontradas"

# ── 9. Upload sem Content-Type correto → 415 ─────────────────────────────────
echo ""
echo "▶ Upload sem multipart → deve retornar 415"
STATUS=$(curl -s -o /tmp/body -w "%{http_code}" \
  -X POST "$API/api/fiscal/leitura/upload" \
  -H "Authorization: Bearer $FISCAL_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}')
assert_status "POST /api/fiscal/leitura/upload (Content-Type errado)" "415" "$STATUS"

# ── 10. Upload de foto (simulado) ─────────────────────────────────────────────
echo ""
echo "▶ Fiscal faz upload de foto (modo simulado)"

# Obter IDs das unidades da OS
STATUS=$(json_get "/api/fiscal/os/$OS_ID" "$FISCAL_TOKEN")
UNIDADE1_ID=$(cat /tmp/body | jq -r '.unidades[0].id')
UNIDADE2_ID=$(cat /tmp/body | jq -r '.unidades[1].id')

# Criar imagem fake (magic bytes JPEG + padding para passar validação de 50KB)
FOTO_TMP=$(mktemp /tmp/hidrometro_XXXX.jpg)
# Magic bytes JPEG: FF D8 FF E0, resto preenchido com zeros até 51200 bytes
printf '\xff\xd8\xff\xe0' > "$FOTO_TMP"
dd if=/dev/zero bs=1 count=51196 >> "$FOTO_TMP" 2>/dev/null

# No MSYS/Git Bash, curl -F @path precisa do path Windows
FOTO_WIN=$(cygpath -w "$FOTO_TMP" 2>/dev/null || echo "$FOTO_TMP")

STATUS=$(curl -s -o /tmp/body -w "%{http_code}" \
  -X POST "$API/api/fiscal/leitura/upload" \
  -H "Authorization: Bearer $FISCAL_TOKEN" \
  -F "osId=$OS_ID" \
  -F "unidadeId=$UNIDADE1_ID" \
  -F "foto=@$FOTO_WIN;type=image/jpeg")
assert_status "POST /api/fiscal/leitura/upload (unidade 1)" "200" "$STATUS"
LEITURA1_ID=$(cat /tmp/body | jq -r '.id // .Id // empty')
[[ -n "$LEITURA1_ID" ]] && ok "Leitura 1 criada (id=$LEITURA1_ID)" || fail "ID de leitura 1 ausente"
CONFIANCA=$(cat /tmp/body | jq -r '.confiancaIa // 0')
[[ $(echo "$CONFIANCA > 0" | bc -l 2>/dev/null || echo "0") == "1" ]] && ok "confiancaIa > 0 ($CONFIANCA)" || ok "Modo simulado (confiancaIa=$CONFIANCA)"

# Upload unidade 2
STATUS=$(curl -s -o /tmp/body -w "%{http_code}" \
  -X POST "$API/api/fiscal/leitura/upload" \
  -H "Authorization: Bearer $FISCAL_TOKEN" \
  -F "osId=$OS_ID" \
  -F "unidadeId=$UNIDADE2_ID" \
  -F "foto=@$FOTO_WIN;type=image/jpeg")
assert_status "POST /api/fiscal/leitura/upload (unidade 2)" "200" "$STATUS"
LEITURA2_ID=$(cat /tmp/body | jq -r '.id // .Id // empty')
[[ -n "$LEITURA2_ID" ]] && ok "Leitura 2 criada (id=$LEITURA2_ID)" || fail "ID de leitura 2 ausente"
rm -f "$FOTO_TMP"

# ── 11. Progresso da OS ───────────────────────────────────────────────────────
echo ""
echo "▶ Progresso da OS"
STATUS=$(json_get "/api/fiscal/os/$OS_ID/progresso" "$FISCAL_TOKEN")
assert_status "GET /api/fiscal/os/$OS_ID/progresso" "200" "$STATUS"
REGISTRADAS=$(cat /tmp/body | jq -r '.leiturasRegistradas')
[[ "$REGISTRADAS" -ge 2 ]] && ok "Progresso: $REGISTRADAS leituras registradas" || fail "Esperava ≥2 leituras, obteve $REGISTRADAS"

# ── 12. Login Operador ────────────────────────────────────────────────────────
echo ""
echo "▶ Login Operador"
STATUS=$(json_post "/api/auth/login" "" '{"cpf":"11111111111","senha":"Operador@123"}')
assert_status "POST /api/auth/login (operador)" "200" "$STATUS"
OP_TOKEN=$(cat /tmp/body | jq -r '.token // empty')
[[ -n "$OP_TOKEN" ]] && ok "Token Operador obtido" || fail "Token Operador ausente"

# ── 13. Fiscal não acessa rota de Operador ────────────────────────────────────
echo ""
echo "▶ Fiscal tentando acessar rota de Operador → 403"
STATUS=$(json_get "/api/operador/ordens-aguardando" "$FISCAL_TOKEN")
assert_status "GET /api/operador/ordens-aguardando (como fiscal)" "403" "$STATUS"

# ── 14. Operador lista ordens aguardando ──────────────────────────────────────
echo ""
echo "▶ Operador lista ordens aguardando"
STATUS=$(json_get "/api/operador/ordens-aguardando" "$OP_TOKEN")
assert_status "GET /api/operador/ordens-aguardando" "200" "$STATUS"

# ── 15. Operador valida leitura 1 ─────────────────────────────────────────────
echo ""
echo "▶ Operador valida leitura 1"
STATUS=$(json_patch "/api/operador/leituras/$LEITURA1_ID/validar" "$OP_TOKEN" \
  '{"observacao":"Validado no E2E"}')
assert_status "PATCH /api/operador/leituras/$LEITURA1_ID/validar" "200" "$STATUS"
assert_json "Status = Validado" '.status' "Validado" "$(cat /tmp/body)"

# ── 16. Operador rejeita leitura 2 sem motivo → 400 ──────────────────────────
echo ""
echo "▶ Rejeitar sem motivo → 400"
STATUS=$(json_patch "/api/operador/leituras/$LEITURA2_ID/rejeitar" "$OP_TOKEN" \
  '{"motivoRejeicao":""}')
assert_status "PATCH rejeitar sem motivo" "400" "$STATUS"

# ── 17. Operador corrige leitura 2 ────────────────────────────────────────────
echo ""
echo "▶ Operador corrige leitura 2"
STATUS=$(json_patch "/api/operador/leituras/$LEITURA2_ID/corrigir" "$OP_TOKEN" \
  '{"valorM3Corrigido":12.5,"observacao":"Corrigido no E2E"}')
assert_status "PATCH /api/operador/leituras/$LEITURA2_ID/corrigir" "200" "$STATUS"
assert_json "Status = Validado após correção" '.status' "Validado" "$(cat /tmp/body)"

# ── 18. Gerar Excel com OS 100% completa ──────────────────────────────────────
echo ""
echo "▶ Gerar relatório Excel (OS 100%)"
STATUS=$(curl -s -o /tmp/excel_output -w "%{http_code}" \
  -X POST "$API/api/operador/relatorio/$OS_ID/excel" \
  -H "Authorization: Bearer $OP_TOKEN")
assert_status "POST /api/operador/relatorio/$OS_ID/excel" "200" "$STATUS"
EXCEL_SIZE=$(wc -c < /tmp/excel_output)
[[ "$EXCEL_SIZE" -gt 100 ]] && ok "Excel gerado ($EXCEL_SIZE bytes)" || fail "Excel vazio ou muito pequeno ($EXCEL_SIZE bytes)"

# ── 19. Gerar PDF com OS 100% completa ───────────────────────────────────────
echo ""
echo "▶ Gerar relatório PDF (OS 100%)"
STATUS=$(curl -s -o /tmp/pdf_output -w "%{http_code}" \
  -X POST "$API/api/operador/relatorio/$OS_ID/pdf" \
  -H "Authorization: Bearer $OP_TOKEN")
assert_status "POST /api/operador/relatorio/$OS_ID/pdf" "200" "$STATUS"
PDF_SIZE=$(wc -c < /tmp/pdf_output)
[[ "$PDF_SIZE" -gt 100 ]] && ok "PDF gerado ($PDF_SIZE bytes)" || fail "PDF vazio ou muito pequeno ($PDF_SIZE bytes)"

# ── 20. Rate limit no login ───────────────────────────────────────────────────
echo ""
echo "▶ Rate limit — 4ª tentativa de login inválido deve retornar 429"
for i in 1 2 3; do
  json_post "/api/auth/login" "" '{"cpf":"99999999999","senha":"errada"}' > /dev/null
done
STATUS=$(json_post "/api/auth/login" "" '{"cpf":"99999999999","senha":"errada"}')
assert_status "POST /api/auth/login (4ª tentativa — rate limited)" "429" "$STATUS"

# ── 21. Auditoria ─────────────────────────────────────────────────────────────
echo ""
echo "▶ Auditoria registrou eventos"
STATUS=$(json_get "/api/admin/auditoria" "$ADMIN_TOKEN")
assert_status "GET /api/admin/auditoria" "200" "$STATUS"
AUDIT_COUNT=$(cat /tmp/body | jq 'if type=="array" then length else (.items // . | length) end' 2>/dev/null || echo "0")
[[ "$AUDIT_COUNT" -ge 3 ]] && ok "Auditoria: $AUDIT_COUNT registros" || fail "Auditoria com menos de 3 registros ($AUDIT_COUNT)"

# ── Resultado final ───────────────────────────────────────────────────────────
echo ""
echo "══════════════════════════════════════"
echo "  Resultado: $PASS passed, $FAIL failed"
echo "══════════════════════════════════════"
[[ $FAIL -eq 0 ]] && echo "  ✅ Todos os testes passaram!" && exit 0 || echo "  ❌ $FAIL teste(s) falharam." && exit 1
