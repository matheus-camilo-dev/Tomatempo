# Tomatempo

Timer pomodoro na web. Funciona anonimamente; com login, desbloqueia meta diária, sequência de dias com
congelamentos, ranking global semanal, estatísticas e galeria de temas.

Timer fixo em 25/5/15, pausa longa a cada 4 ciclos, sem emenda automática entre ciclos.

## Documentação

| Documento | Papel |
|---|---|
| [`docs/REGRAS-DE-NEGOCIO.md`](docs/REGRAS-DE-NEGOCIO.md) | Fonte da verdade do produto. Vence qualquer outro documento em conflito. |
| [`docs/ESPECIFICACAO-TECNICA.md`](docs/ESPECIFICACAO-TECNICA.md) | Arquitetura, modelo de dados, API, processo de teste, segurança. |
| [`SECURITY.md`](SECURITY.md) | Como reportar vulnerabilidades. |

## Começando

```bash
git clone <este-repo> && cd tomatempo
git config core.hooksPath .githooks   # obrigatório: hook que barra segredo no commit
cp .env.example .env

cd src/web
npm install
npm run dev
```

Recomendado instalar o [gitleaks](https://github.com/gitleaks/gitleaks) — o hook usa uma varredura embutida
mais fraca quando ele não está presente. Para conferir que o hook está ativo:

```bash
# O token é gerado na hora: um literal aqui faria o próprio README ser barrado.
printf 'GITHUB_TOKEN=ghp_%s\n' "$(tr -dc 'A-Za-z0-9' </dev/urandom | head -c 36)" > leak-probe.txt
git add leak-probe.txt && .githooks/pre-commit   # deve bloquear
git restore --staged leak-probe.txt && rm leak-probe.txt
```

## Comandos

```bash
npm run dev            # servidor de desenvolvimento
npm test               # suíte completa
npm run test:watch     # modo observador
npm run test:coverage  # com cobertura (gate de 90% no domínio)
npm run test:e2e       # E2E em Chromium, contra o build de produção (só o front)
npm run test:e2e:api   # E2E da fase 2: conta e ciclo logado, contra a API e o Postgres
npm run typecheck      # TypeScript strict
npm run lint           # oxlint
npm run build          # build de produção
```

## Como se contribui aqui

Três regras não negociáveis, detalhadas na especificação técnica:

1. **Teste antes da feature** (§6). O commit de teste vermelho vem antes do commit de implementação, e testes
   existentes não são alterados durante a implementação.
2. **Segurança verificada a cada mudança** (§7). OWASP Top 10 e os cinco padrões de código gerado por IA, com
   checklist obrigatório. Nenhum segredo entra no repositório — ele é público desde o primeiro commit.
3. **Documentação junto com a regra** (§8). Regra de negócio nova ou alterada atualiza os arquivos de
   especificação no mesmo trabalho.

## Estado atual

**Fase 1 completa**: timer 25/5/15 sem emenda automática, histórico local do dia, alarme e notificação,
tema claro/escuro, português e inglês, e PWA instalável que abre offline.

**Fase 2 completa**: conta com e-mail/senha e Google, confirmação de e-mail, meta diária com efeito no dia
seguinte, sequência de dias com congelamentos, sessões de foco validadas no servidor e fila offline que
sobe o que foi cumprido sem rede.

Fase 3 — ranking semanal, perfil público, conquistas e estatísticas pessoais — descrita na especificação
técnica, §5.

Antes do primeiro `npm run test:e2e`, instale o navegador uma vez:

```bash
npx playwright install chromium
```

O E2E da fase 2 conversa com a API de verdade, então ele precisa do banco e do captador de e-mail de pé.
O Playwright sobe a API e o build do front sozinho; o resto é uma linha:

```bash
docker compose up -d postgres mailpit
npm run test:e2e:api
```

Ele usa o banco `tomatempo_e2e`, criado na subida pela migração e separado do banco de desenvolvimento —
a suíte cria contas a cada caso e não suja aquele em que você trabalha.

## Licença

[MIT](LICENSE) © 2026 Matheus Camilo Velanzuela Meira
