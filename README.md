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
mais fraca quando ele não está presente.

## Comandos

```bash
npm run dev            # servidor de desenvolvimento
npm test               # suíte completa
npm run test:watch     # modo observador
npm run test:coverage   # com cobertura (gate de 90% no domínio)
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

Fase 1 em andamento (timer anônimo + PWA). Fases 2 e 3 — conta, meta, streak, ranking — descritas na
especificação técnica, §5.

## Licença

A definir.
