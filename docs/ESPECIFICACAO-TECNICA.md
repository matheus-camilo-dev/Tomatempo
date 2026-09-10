# Tomatempo — Especificação de Software

> Documento de engenharia. A fonte da verdade **de produto** é `docs/REGRAS-DE-NEGOCIO.md`;
> este arquivo traduz aquelas regras em arquitetura, modelo de dados, contratos de API e fases de entrega.
> Consolidado em 02/09/2026. Em caso de conflito entre os dois, o documento de regras vence e este é corrigido.

## 0. Decisões travadas nesta sessão

| Tema | Decisão |
|---|---|
| Nome | **Tomatempo** |
| Stack | React SPA (Vite + TypeScript) + API ASP.NET Core (.NET 10 LTS) |
| Banco | PostgreSQL + EF Core (Npgsql) |
| Auth | ASP.NET Core Identity (e-mail/senha) + Google OAuth |
| UI | Tailwind CSS + shadcn/ui |
| i18n | pt-BR e en desde a fase 1 |
| Repositório | Monorepo (`src/api` + `src/web`) |
| Deploy | Docker local primeiro (`docker compose`); nuvem decidida depois, sem impacto no código |
| Entrega | 3 fases: timer anônimo → conta/meta/streak → ranking/perfil |
| Meta diária | Perguntada no cadastro (2 / 4 / 6 / 8, padrão 4) |
| Congelamentos | 2 na conta nova, +1 a cada 7 dias consecutivos de meta batida, + premiação semanal; teto 5 |
| Freeze em dia parado | Consome 1 freeze mesmo sem nenhuma atividade no dia |
| Validação de foco | `start` + heartbeat ~30s + `complete`/`abandon`, sempre pelo relógio do servidor |
| Concorrência | 1 sessão de foco ativa por usuário; nova sessão assume e encerra a anterior |
| Privacidade | Opt-out **total** do ranking (não pontua, não vê posição, não ganha freeze de ranking) |
| Offline | Sincroniza com teto e janela; conta para meta/streak, nunca para ranking |
| Perfil | Apelido único (URL) + avatar de galeria curada |
| Conquistas | 4 famílias: streak, volume de foco, ranking, hábito |
| Processo de teste | **TDD obrigatório**: teste antes da feature, em domínio, API e componentes (§6) |
| Teste no back | xUnit + AwesomeAssertions + NSubstitute + Testcontainers |
| Teste no front | Vitest + Testing Library + user-event + MSW |
| Cobertura | Gate de 90% no domínio (back e front); resto medido sem barrar |
| Repositório público | **Sim, desde o commit 1** — gitleaks e push protection antes do primeiro push |
| Segurança | OWASP Top 10 + 5 padrões de código gerado por IA, verificados a cada mudança (§7) |
| SAST | CodeQL + gitleaks no CI, bloqueantes em achado alto/crítico |
| Segredos | `dotnet user-secrets` e `.env` em dev, GitHub Secrets no CI, env vars em produção |
| Dependências | Dependabot; build falha com CVE alto ou crítico |
| Documentação | Regra nova ou alterada atualiza os 3 arquivos de especificação no mesmo trabalho (§8) |

---

## 1. Arquitetura

```
tomatempo/
├─ docker-compose.yml          # api + web + postgres + mailpit
├─ docs/
│  ├─ REGRAS-DE-NEGOCIO.md     # produto (PO)
│  └─ ESPECIFICACAO-TECNICA.md # este arquivo
├─ src/
│  ├─ api/
│  │  ├─ Tomatempo.Domain/          # entidades + regras puras, sem I/O
│  │  ├─ Tomatempo.Infrastructure/  # EF Core, migrations, jobs, e-mail
│  │  ├─ Tomatempo.Api/             # Minimal APIs, auth, DI, OpenAPI
│  │  ├─ Tomatempo.Domain.Tests/    # unitários puros, sem I/O, rodam em ms
│  │  └─ Tomatempo.Api.Tests/       # integração: WebApplicationFactory + Testcontainers
│  └─ web/                          # Vite + React 19 + TS
└─ .github/workflows/ci.yml
```

**Princípio central:** toda regra que mexe na "moeda" do jogo (minutos de ranking, streak, congelamentos, meta)
é função pura em `Tomatempo.Domain`, sem dependência de banco ou de relógio ambiente — o tempo entra como
parâmetro. É isso que torna o antifraude testável.

**Como serve em produção:** um único origin. Reverse proxy (nginx no container do `web`) serve o SPA estático e
faz proxy de `/api/*` para a API. Isso mantém o cookie de sessão `SameSite=Lax` sem precisar de CORS. Em dev,
Vite em `:5173` com proxy para a API em `:8080`.

### 1.1 Front-end

| Peça | Escolha |
|---|---|
| Build | Vite + React 19 + TypeScript strict |
| Estilo | Tailwind + shadcn/ui, tokens de tema em CSS variables |
| Estado de servidor | TanStack Query |
| Estado do timer | Zustand (store única, persistida em localStorage) |
| Rotas | React Router |
| i18n | i18next + react-i18next, `pt-BR` e `en`, detecção pelo navegador |
| PWA | vite-plugin-pwa (Workbox), manifest + service worker |
| Fila offline | IndexedDB (idb) |
| Testes | Vitest + Testing Library + user-event + MSW (ver §6) |

### 1.2 Back-end

| Peça | Escolha |
|---|---|
| Runtime | .NET 10 LTS, Minimal APIs |
| ORM | EF Core + Npgsql, migrations versionadas |
| Auth | Identity com cookie `HttpOnly` + `SameSite=Lax`; Google via authorization code |
| Jobs | `BackgroundService` com advisory lock no Postgres (`pg_try_advisory_lock`) |
| Rate limit | `Microsoft.AspNetCore.RateLimiting` em auth e sessões |
| Log | Serilog em JSON, com `TraceId` |
| Doc | OpenAPI gerado; cliente TS do front tipado a partir dele |
| Relógio | `TimeProvider` injetado em tudo; `DateTime.UtcNow` proibido fora da infra |
| Testes | xUnit + AwesomeAssertions + NSubstitute + Testcontainers (ver §6) |

---

## 2. Modelo de dados

Datas: **tudo em UTC no banco** (`timestamptz`). O "dia local" do usuário é gravado como `date` já resolvido no
momento do registro — nunca recalculado a partir de UTC depois.

### `users` (estende AspNetUsers)

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid | PK |
| `email`, `password_hash`, ... | | Identity |
| `email_confirmed` | bool | **pré-requisito para pontuar no ranking** |
| `nickname` | citext unique | 3–20 chars, `[a-z0-9_]`, define a URL `/u/{nickname}`; `null` até o onboarding |
| `avatar_id` | text | id do catálogo estático |
| `time_zone` | text | IANA (ex.: `America/Sao_Paulo`) |
| `locale` | text | `pt-BR` \| `en` |
| `color_mode` | text | `light` \| `dark` \| `system` |
| `theme_preset_id` | text | catálogo estático; `null` = padrão |
| `ranking_opt_out` | bool | default `false` |
| `nickname_changed_at` | timestamptz? | trava de 30 dias para a próxima troca (§11.8) |
| `onboarding_completed_at` | timestamptz? | `null` = apelido/meta/fuso ainda não escolhidos; conta não pontua |
| `created_at` | timestamptz | |

Logins externos (Google) ficam em `AspNetUserLogins`, do próprio Identity — sem tabela própria.

### `user_goals` (histórico — meta só vale no dia seguinte)

`id`, `user_id`, `pomodoros` (int 1–24), `effective_from` (date local), `created_at`.

Meta vigente em um dia D = linha de maior `effective_from <= D`. Uma edição hoje grava `effective_from = D+1`;
uma segunda edição no mesmo dia **substitui** a linha futura em vez de empilhar.

### `focus_sessions`

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid | |
| `user_id` | uuid | |
| `started_at_utc` | timestamptz | **relógio do servidor**, quando online |
| `ended_at_utc` | timestamptz? | |
| `last_heartbeat_at_utc` | timestamptz? | |
| `status` | enum | `Active` \| `Completed` \| `Abandoned` \| `Expired` \| `Superseded` |
| `source` | enum | `Online` \| `Offline` |
| `counted_minutes` | int | minutos válidos apurados (0 se descartada) |
| `ranking_eligible` | bool | `Online && counted >= 5 && email_confirmed && !opt_out` |
| `local_date` | date | dia local do usuário no início da sessão |
| `client_session_id` | uuid? | idempotência do lote offline; único por usuário |

Índices: `(user_id, local_date)`, parcial em `status = Active` por `user_id`, e `(started_at_utc)`.

Pausas **não** são persistidas — não pontuam e não alimentam nenhuma regra.

### `daily_stats` (uma linha por usuário/dia local)

`user_id`, `local_date` (PK composta), `completed_pomodoros`, `focus_minutes`, `online_focus_minutes`, `goal`,
`goal_met`, `freeze_used`, `closed_at`.

Atualizada incrementalmente a cada sessão apurada e **selada** pelo job de fechamento do dia.

### `user_streaks` (1:1 com user)

`user_id`, `current_streak`, `longest_streak`, `last_closed_local_date`, `freeze_balance` (0–5),
`consecutive_goal_days` (contador do marco de 7).

### `freeze_ledger` (auditoria e UI de "de onde vieram")

`id`, `user_id`, `delta` (+/-), `reason` (`Signup` | `StreakMilestone` | `WeeklyRanking` | `DayCovered` |
`CapOverflow`), `ref_date`, `created_at`. O saldo é sempre reconciliável somando o ledger.

### `weekly_rankings`

`week_id` (`2026-W36`, ISO em UTC), `user_id` (PK composta), `focus_minutes`, `eligible`, `rank`,
`freezes_awarded`, `closed_at`. Índice `(week_id, rank)`.

### `user_achievements`

`user_id`, `code` (PK composta), `unlocked_at`, `progress` (jsonb). As definições ficam **em código**, não no banco.

### Catálogos estáticos (em código, versionados)

Temas preset, avatares e definições de conquistas. Nada disso é editável por usuário.

---

## 3. Regras de domínio (implementação)

### 3.1 Ciclo do timer

Durações fixas: foco 25min, pausa curta 5min, pausa longa 15min a cada 4 pomodoros **concluídos no dia local**.
Nunca configuráveis — nem por query string, nem por preferência escondida.

**Sem emenda automática:** ao fim de qualquer ciclo o app para, toca o alarme, notifica e espera clique. Nenhum
`setTimeout` inicia o ciclo seguinte.

**Precisão:** o timer nunca decrementa um contador. Guarda o `endsAt` absoluto e recalcula o restante a cada
250ms a partir de `Date.now()`. Assim sobrevive a throttling de aba, sleep do sistema e troca de janela. Ao
voltar do background, recalcula e, se o fim já passou, dispara a conclusão pendente.

### 3.2 Apuração de minutos (online)

```
POST /sessions/start          → servidor grava started_at_utc (relógio dele)
POST /sessions/{id}/beat      → a cada 30s
POST /sessions/{id}/complete  (ou /abandon)
```

- `elapsed = min(serverNow, ended_at) - started_at_utc`
- **Cadeia de heartbeats**: se houve intervalo maior que 90s entre dois heartbeats, os minutos são truncados no
  último heartbeat antes do buraco. Aba fechada não rende tempo fantasma.
- `counted = floor(min(elapsed_válido, 25min))` em minutos.
- `counted < 5` → descartada (`counted_minutes = 0`, não conta para nada).
- Conclusão exige `counted >= 25` **e** heartbeat a menos de 90s do fim; caso contrário vira `Abandoned` parcial.
- **Varredor**: job a cada 60s marca como `Expired` toda sessão `Active` sem heartbeat há mais de 2min, apurando
  o parcial pelas regras acima.
- **Teto diário**: no máximo 16 sessões apuradas por dia local (online + offline). O excedente é registrado com
  `counted_minutes = 0`.

### 3.3 Sessão única

`start` com uma sessão `Active` existente: a antiga é encerrada como `Superseded` (o parcial dela é apurado
normalmente) e a nova começa. O cliente antigo descobre no próximo heartbeat (`409`) e mostra "esta sessão foi
assumida em outro dispositivo".

### 3.4 Offline

O cliente enfileira em IndexedDB `{clientSessionId, startedAtLocal, offsetMinutes, durationSeconds}` e envia em
lote ao reconectar. A fila só recolhe ciclo de quem está logado — o do anônimo não migra (§3.10) — e é
**descartada no logout**: nenhum identificador de usuário pode ser guardado no navegador (§7.2/A01), então a
fila não sabe de quem é, e sobreviver ao logout significaria drenar na próxima conta que entrasse ali. O servidor aceita a sessão se, e somente se:

1. `startedAt` está dentro das últimas **48h** e não no futuro (tolerância de 2min);
2. `duration` está entre 5 e 25min;
3. não sobrepõe nenhuma sessão já registrada do usuário;
4. o dia local resultante não excede 16 sessões;
5. `clientSessionId` é inédito (idempotência — reenvio é no-op).

O lote traz no máximo **100 itens**. Não é regra de produto: 48h de uso cabem folgado em 100 sessões, e o teto
existe para que um lote inventado não vire trabalho ilimitado no servidor (§7.2/A04). Lote maior é recusado
inteiro, com `invalid_request`.

Aceitas com `source = Offline` e `ranking_eligible = false`. Contam para meta e streak. As rejeitadas retornam
motivo por item, e o cliente descarta sem retry.

### 3.5 Meta diária

Unidade: **pomodoros concluídos** (25min cheios), não minutos. Parciais não contam para a meta — só para minutos
de foco e ranking.

No cadastro o usuário escolhe 2/4/6/8 (padrão 4); pode alterar depois, valendo sempre a partir do dia seguinte.

### 3.6 Fuso e fechamento do dia

- Dia = fuso IANA do usuário. **Mudança de fuso só vale no dia seguinte** (mesma proteção da meta), evitando
  ganhar ou perder um dia ao viajar.
- Job roda a cada hora em `:05` UTC e processa os usuários cuja meia-noite local acabou de passar — isso cobre
  offsets de :30 e :45 e horário de verão, desde que via biblioteca de tz e nunca por aritmética de offset fixo.
- Fechamento de um dia D:
  1. sela `daily_stats[D]`, com `goal_met = completed_pomodoros >= goal(D)`;
  2. se `goal_met` → `current_streak += 1`, `consecutive_goal_days += 1`;
  3. senão, se `freeze_balance > 0` → consome 1 (ledger `DayCovered`), `current_streak += 1` e
     **`consecutive_goal_days = 0`** (dia coberto não conta para o marco de 7 — senão dá para farmar freeze com freeze);
  4. senão → `current_streak = 0`, `consecutive_goal_days = 0`;
  5. se `consecutive_goal_days` chegou a múltiplo de 7 → +1 freeze (`StreakMilestone`);
  6. `longest_streak = max(longest_streak, current_streak)`;
  7. todo crédito respeita o teto de 5 — o excedente vira linha `CapOverflow` com `delta = 0`.
- Idempotente: reprocessar um dia já fechado não altera nada (`closed_at` como guarda).

### 3.7 Ranking semanal

- Janela: segunda 00:00:00 UTC a domingo 23:59:59.999 UTC (semana ISO), igual para o planeta inteiro.
- Métrica: soma de `counted_minutes` das sessões com `ranking_eligible = true` iniciadas na janela.
- Elegibilidade: bateu a meta em **≥ 1 dia local** cujo fechamento caiu dentro da semana, e-mail confirmado e
  `ranking_opt_out = false`.
- Fechamento (job na segunda, 00:05 UTC): ordena por minutos desc, desempata por `MIN(started_at)` da primeira
  sessão da semana (quem chegou lá antes fica na frente), grava `weekly_rankings` e credita:

  | Posição | Freezes |
  |---|---|
  | 1–20 | 3 |
  | 21–50 | 2 |
  | 51–100 | 1 |
  | 101+ | 0 |

- Leitura ao vivo durante a semana: view materializada com o top 100, atualizada a cada 5min, mais consulta
  pontual da própria posição. Nunca um `ORDER BY` na tabela de sessões dentro de request de usuário.
- **Opt-out total**: enquanto ligado, as sessões nascem com `ranking_eligible = false`, o usuário não aparece na
  lista, não vê posição, não recebe freeze de ranking e `/u/{nickname}` responde 404. Religar passa a valer da
  ativação em diante — os minutos anteriores da semana não são recuperados.

### 3.8 Conquistas

Avaliadas no fechamento do dia e no fechamento da semana; nunca em request de usuário.

| Família | Códigos |
|---|---|
| Streak | `streak_7`, `streak_30`, `streak_100`, `streak_365` |
| Volume | `focus_10h`, `focus_50h`, `focus_100h`, `focus_500h` |
| Ranking | `rank_top100`, `rank_top50`, `rank_top20`, `rank_first`, `weeks_awarded_5`, `weeks_awarded_20` |
| Hábito | `first_pomodoro`, `perfect_week` (7/7 dias com meta batida, sem freeze), `long_break_day` (4 pomodoros no mesmo dia), `early_bird` (pomodoro concluído antes das 7h locais), `night_owl` (depois das 23h locais) |

### 3.9 Anônimo

Zero chamadas ao servidor. Timer, tema claro/escuro e histórico do dia ficam em localStorage. A tela mostra o CTA
de cadastro. **Nada é migrado no cadastro** — a conta nasce zerada, e a UI avisa isso explicitamente antes de
criar a conta, para não parecer bug.

### 3.10 Conta, autenticação e onboarding

- **Cadastro por e-mail/senha** grava o usuário, a primeira linha de `user_goals` com `effective_from` = dia
  local de hoje (é a meta inicial, não uma edição — a regra do dia seguinte vale para *alterações*), 2 créditos
  `Signup` no `freeze_ledger` e `user_streaks` zerado. A sessão sai na resposta: o usuário entra logado com
  `email_confirmed = false`.
- **Confirmação de e-mail**: token aleatório guardado como hash, uso único, validade
  `EmailConfirmationTokenLifetime = 24h`. Reenvio permitido dentro do rate limit. Consumir um token invalida
  os demais pendentes do mesmo usuário e tipo — senão um link antigo, capturado antes, sobreviveria ao uso do
  link novo.
- **Reset de senha**: mesmo desenho, validade `PasswordResetTokenLifetime = 1h`; ao trocar a senha, todas as
  sessões anteriores do usuário são invalidadas.
- **Sem enumeração de conta**: `/auth/login` e `/auth/forgot-password` respondem exatamente igual para e-mail
  conhecido e desconhecido, e a resposta de credencial inválida é a mesma para "e-mail não existe" e "senha
  errada". `/auth/register` devolve o mesmo corpo nos dois casos e, quando o e-mail já tem conta, envia um
  aviso ao dono em vez de criar a segunda. **Resíduo aceito** (log de 03/09/2026): o cookie de sessão só
  acompanha o caso de conta nova, e isso é observável; a contenção é o rate limit de 5/min.
- **Google (authorization code)**: o callback exige `email_verified = true` do provedor. E-mail já cadastrado →
  vincula o login externo à conta existente e entra nela. E-mail novo → cria a conta com
  `email_confirmed = true` e `onboarding_completed_at = null`.
- **Onboarding**: enquanto `onboarding_completed_at` for `null`, `/me` responde `onboardingRequired: true` e os
  endpoints de sessão respondem `409` com `code = onboarding_required`. `POST /auth/onboarding` é a única saída
  desse estado e só aceita ser chamado uma vez.
- **Apelido**: 3–20 caracteres de `[A-Za-z0-9_]` na entrada, **normalizado para minúsculas** antes de gravar —
  o valor guardado e a URL pública obedecem sempre a whitelist `[a-z0-9_]`. Unicidade não distingue maiúsculas
  (`citext`): com `ana_1` cadastrado, `ANA_1` é apelido repetido, não apelido inválido. Nova troca só depois de
  `NicknameChangeLockDays = 30` dias da anterior (§11.8).
- **Senha**: entre `PasswordMinLength = 8` e `PasswordMaxLength = 128` caracteres, e recusa de senha que esteja
  na lista de senhas comuns embutida na aplicação, tanto no cadastro quanto no reset (§7.2/A07). A lista é um
  arquivo versionado, carregado uma vez em memória; ampliá-la não muda código. O teto existe porque a senha
  alimenta um hash caro de propósito (Argon2id): sem limite, o tamanho da entrada vira alavanca de consumo.
- **E-mail**: no máximo `EmailMaxLength = 254` caracteres, o limite de endereço da RFC 5321. Sem isso, um
  endereço maior que a coluna estoura no banco e vira erro interno em vez de recusa explícita.
- **Rate limit do login particionado por e-mail** (5/min, §4), não por IP: o alvo do ataque é uma conta
  específica, e a partição por e-mail não revela nada — a resposta continua idêntica para conta existente e
  inexistente.
- **Sessão**: cookie do Identity `HttpOnly` + `Secure` + `SameSite=Lax`, regenerado no login e invalidado no
  logout. O `userId` sai sempre da sessão, nunca de payload, rota ou query (§7.2/A01).
- **Google**: o fluxo authorization code usa **PKCE**. Se a criação ou o vínculo da conta falhar no meio do
  caminho, o usuário volta para `/entrar?erro=external_login_failed` — nunca para uma sessão aberta sobre uma
  conta que não terminou de nascer.
- **Exclusão de conta** (`DELETE /me`): exige a sessão e, quando a conta tem senha, **a senha atual no corpo** —
  sessão roubada não apaga conta sozinha. Remove o usuário e tudo que é dele (metas, ledger, streak, tokens e
  vínculos externos, por cascata no banco); e-mail e apelido voltam a ficar livres. Conta criada pelo Google,
  que não tem senha, é excluída só com a sessão. A anonimização das linhas de ranking já fechadas (§7.7) entra
  junto com o ranking, na fase 3. Para a tela saber qual dos dois casos pedir, `GET /me` devolve `hasPassword`
  — um booleano derivado, nunca o hash, uma dica ou qualquer parte da senha; sem ele o front só descobriria
  tentando excluir e apanhando 401, o que é pedir confirmação de exclusão duas vezes.
- Constantes em `Tomatempo.Domain` (§8.4): `SignupFreezeGrant = 2`, `DefaultDailyGoal = 4`,
  `AllowedDailyGoals = {2,4,6,8}`, `NicknameMinLength = 3`, `NicknameMaxLength = 20`,
  `NicknameChangeLockDays = 30`, `EmailConfirmationTokenLifetime = 24h`, `PasswordResetTokenLifetime = 1h`.

---

## 4. API (v1, prefixo `/api`)

**Auth**

```
POST   /auth/register            {email, password, nickname, goal, timeZone, locale} → sessão (conta nova)
POST   /auth/login               {email, password}
POST   /auth/logout
GET    /auth/google              → redirect
GET    /auth/google/callback     → vincula ou cria; leva ao onboarding se a conta estiver incompleta
POST   /auth/onboarding          {nickname, goal, timeZone, locale} → só enquanto onboardingRequired
POST   /auth/confirm-email       {token}
POST   /auth/resend-confirmation → 202 sempre, com ou sem conta naquele e-mail
POST   /auth/forgot-password     {email}
POST   /auth/reset-password      {token, password}
GET    /me                       → perfil, onboardingRequired, hasPassword, meta vigente e futura, streak, freezes, preferências
PATCH  /me                       {nickname?, avatarId?, timeZone?, locale?, colorMode?, themePresetId?, rankingOptOut?}
PUT    /me/goal                  {pomodoros} → responde effectiveFrom (sempre amanhã)
GET    /me/freezes               → saldo + ledger explicado (a origem de cada crédito)
DELETE /me                       → exclusão de conta
```

**Sessões**

```
POST   /sessions/start           {kind:"focus"} → {sessionId, startedAt, endsAt, serverNow}
POST   /sessions/{id}/beat       → {ok, serverNow, remainingSeconds} | 409 se Superseded/Expired
POST   /sessions/{id}/complete   → {countedMinutes, completedToday, goal, goalMet}
POST   /sessions/{id}/abandon    → {countedMinutes, discarded}
POST   /sessions/offline-batch   {items:[...]} → por item: {clientSessionId, accepted, reason?}
GET    /sessions/active          → sessão ativa (recuperação após reload ou troca de dispositivo)
```

**Estatísticas e ranking**

```
GET    /stats/today
GET    /stats/history?from&to    → série diária (minutos, pomodoros, meta, goalMet, freezeUsed)
GET    /ranking/current?page     → top 100 paginado + minha posição
GET    /ranking/weeks/{weekId}
GET    /u/{nickname}             → perfil público (404 se opt-out ou inexistente)
GET    /catalog/themes | /catalog/avatars | /catalog/achievements
```

**Convenções:** erros em `application/problem+json` com um `code` estável — o front traduz pelo `code`, nunca
pela mensagem. Rate limit: 5/min por e-mail em cadastro, login e recuperação de senha; **60/min por IP em todo
endpoint anônimo de autenticação**, o que cobre confirmação e reset, que não têm e-mail no corpo para
particionar; 60/min em heartbeat; 20/min por conta no resto.

---

## 5. Fases de entrega

### Fase 1 — Timer anônimo (sem backend)

Escopo: timer 25/5/15 com a regra dos 4 ciclos, sem emenda automática, alarme, notificação do navegador,
claro/escuro, i18n pt-BR/en, PWA instalável e offline-first, histórico local do dia, CTA de cadastro.

O idioma é detectado do navegador e trocável na própria tela, por um seletor compacto — a tela de preferências
só existe a partir da fase 2, e sem alternância não há como o usuário corrigir uma detecção errada. A escolha
fica no `localStorage`; na fase 2 ela passa a acompanhar a conta.

**Pronto quando:**

- [x] Aba em segundo plano por 25min conclui no instante certo (erro < 1s) e notifica.
- [x] Recarregar a página no meio do ciclo restaura o tempo restante correto.
- [x] Fim de ciclo nunca inicia o próximo sozinho.
- [x] App instala como PWA e abre 100% offline.
- [x] Lighthouse PWA e Acessibilidade ≥ 90.
- [x] O manifesto declara nome, ícones de 192 e 512, cor de tema e `display: standalone`.
- [x] Perder a conexão mostra aviso visível; reconectar faz o aviso sumir.
- [x] O timer continua contando e creditando o dia enquanto offline.
- [x] Zero strings hardcoded na UI, incluindo as mensagens de notificação.
- [x] Os dois catálogos têm exatamente as mesmas chaves, sem valor vazio — verificado por teste.
- [x] Trocar o idioma muda a tela inteira e sobrevive ao recarregamento.
- [x] Sem escolha do usuário, o tema segue o sistema — e acompanha a mudança dele sem recarregar.
- [x] Escolher claro ou escuro sobrepõe o sistema e sobrevive ao recarregamento.
- [x] Voltar para "sistema" descarta a escolha e volta a seguir o sistema.
- [x] O seletor de tema indica a opção corrente para leitores de tela.

**Como cada um foi verificado** (02/09/2026): 168 testes unitários e 7 E2E em Chromium real, contra o build de
produção. O E2E cobre service worker ativo, manifesto servido, app abrindo com a rede cortada e o aviso de
offline aparecendo e sumindo. Acessibilidade auditada com axe-core nas regras WCAG 2.1 A e AA: **zero violações
nos dois temas**, 17 regras passando.

Três pontos continuam dependendo de verificação manual, e não são cobertos por automação:

1. **Alarme audível e notificação do sistema** — exigem permissão concedida e alto-falante; os testes provam que
   as chamadas acontecem no momento certo, não que o som sai.
2. **Instalação real** ("adicionar à tela de início") em Android e iOS.
3. **Aba estrangulada por 25 minutos de verdade** — a lógica de timestamp absoluto e de ressincronização está
   testada, mas nenhum teste espera 25 minutos reais.

### Fase 2 — Conta, meta e streak

Escopo: Identity + Google, confirmação de e-mail, onboarding com meta, sessões validadas no servidor
(start/beat/complete/abandon + varredor), sessão única, sincronização offline, meta com efeito no dia seguinte,
fechamento diário, streak, congelamentos com ledger, estatísticas pessoais e galeria de temas.

**Pronto quando:**

- [x] O cadastro credita 2 freezes com linha `Signup` no ledger, streak 0 e nenhum minuto.
- [x] Cadastro com e-mail já usado responde igual ao de e-mail novo e não cria a segunda conta.
- [x] Login e recuperação respondem igual para e-mail existente e inexistente; senha errada e e-mail
      inexistente devolvem o mesmo `code`.
- [x] Sexta tentativa de login no mesmo minuto responde 429.
- [x] Sexta tentativa de cadastro com o mesmo e-mail no mesmo minuto responde 429 — é o limite que
      contém o resíduo de enumeração do cadastro.
- [x] Senha com menos de 8 caracteres ou presente na lista de senhas comuns é recusada no cadastro e no reset.
- [x] Token de confirmação serve uma vez; reusado ou passadas 24h, responde erro estável sem confirmar nada.
- [x] Token de reset serve uma vez, expira em 1h, e trocar a senha derruba as sessões anteriores.
- [x] O cookie de sessão sai `HttpOnly`, `Secure` e `SameSite=Lax`; o logout o invalida.
- [x] Endpoint autenticado sem cookie responde 401, e identificador de usuário vindo do cliente é ignorado.
- [x] Google com e-mail já cadastrado entra na conta existente; sem `email_verified` do provedor, é recusado.
- [x] Conta criada pelo Google responde `onboardingRequired` até escolher apelido, meta e fuso, e nesse estado
      os endpoints de sessão respondem 409.
- [x] Apelido fora da whitelist, curto demais, longo demais ou repetido em outra caixa é recusado.
- [x] `PUT /me/goal` responde `effectiveFrom` = amanhã local; segunda edição no mesmo dia substitui a linha
      futura em vez de empilhar.
- [x] Todo erro sai em `application/problem+json`, com `code` estável e sem stack trace.
- [x] Resposta de erro carrega os mesmos cabeçalhos de segurança que a de sucesso.
- [x] A senha é guardada em Argon2id; conta com hash PBKDF2 antigo continua entrando e sai do login já
      regravada em Argon2id.
- [x] Senha acima de 128 caracteres e e-mail acima de 254 são recusados com `code` estável, não com erro interno.
- [x] Consumir um token de confirmação ou de reset invalida os demais pendentes do mesmo tipo.
- [x] Endpoint anônimo de autenticação responde 429 ao passar de 60 requisições por minuto vindas do mesmo IP.
- [x] Excluir a conta exige a senha atual quando existe uma; a sessão morre, e e-mail e apelido voltam a ficar
      livres para um cadastro novo.
- [x] Conta criada pelo Google, que não tem senha, é excluída só com a sessão.
- [x] Endpoint que muda estado recusa requisição com `Content-Type` de formulário.
- [x] Nenhuma resposta traz o cabeçalho `Server`.
- [x] O fluxo do Google está configurado com PKCE.
- [x] Fechar a aba no minuto 20 credita exatamente 20 minutos; fechar no minuto 3 credita 0.
- [ ] Abrir o timer em duas abas: a segunda assume, a primeira avisa e para de pontuar.
- [x] Manipular o relógio do sistema não altera minuto algum.
- [x] Baixar a meta às 23h não salva a streak do dia; a nova meta aparece só amanhã.
- [x] Dia sem meta batida consome 1 freeze e mantém a streak; sem freeze, zera.
- [x] 7 dias consecutivos de meta batida creditam +1 freeze; dia coberto por freeze reseta o contador do marco.
- [x] Teto de 5 respeitado, com `CapOverflow` registrado.
- [x] Modo avião: 3 pomodoros contam para meta/streak ao reconectar e nenhum minuto vai para o ranking.
- [x] Lote acima de 100 itens é recusado inteiro, com `invalid_request`.
- [x] Reenviar o mesmo lote offline não duplica nada.
- [x] Conta nova criada após uso anônimo começa zerada.
- [x] Suíte de domínio cobre virada de dia, horário de verão, offsets :30 e :45 e virada de ano.

**Pronto quando — front da conta (a tela conversando com a API):**

- [x] Visitante anônimo usa o timer sem nenhuma chamada à API; o CTA de cadastro é o único caminho para a conta.
- [x] Cadastro pela tela já entra logado e mostra o aviso de e-mail não confirmado, com o botão de reenviar.
- [x] Erro da API vira mensagem traduzida a partir do `code`; texto cru vindo do servidor nunca chega à tela.
- [x] Conta com onboarding pendente cai na tela de onboarding e não sai dela sem apelido, meta e fuso.
- [x] Recarregar com sessão válida reidrata o usuário por `GET /me`, sem pedir login de novo.
- [x] 401 com `code` `unauthenticated` em qualquer chamada derruba o estado local de sessão e devolve a
      pessoa à tela pública; 401 de senha errada na exclusão, que traz `invalid_credentials`, não desloga.
- [x] Sair encerra a sessão no servidor e limpa o estado local, mesmo que a chamada de logout falhe.
- [x] A tela da conta mostra a meta corrente e, havendo edição pendente, a data em que a nova meta passa a valer.
- [x] O saldo e o extrato de congelamentos vêm de `GET /me/freezes`, nunca de contagem feita no cliente.
- [x] A identidade nunca sai do cliente: nenhum identificador de usuário é guardado ou enviado; o cookie decide.
- [x] O fuso proposto no onboarding vem do navegador e continua editável antes de confirmar.
- [x] Excluir a conta pede a senha quando a conta tem uma, e não pede quando a conta veio do Google.
- [x] `GET /me` diz se a conta tem senha, sem devolver hash, dica ou qualquer parte dela.
- [x] Volta do Google com erro cai em `/entrar` com a mensagem traduzida do `erro` da URL; conta já completa
      cai no timer.
- [x] `/confirmar-email` consome o token da URL e mostra o resultado; token recusado vira mensagem, não tela
      quebrada.
- [x] Pedir link de recuperação mostra a mesma tela com ou sem conta no endereço — o front não revela nada que
      a API esconde.
- [x] `/nova-senha` troca a senha usando o token da URL e leva a pessoa para o login.

**Pronto quando — o timer logado conversando com a API:**

- [ ] Visitante anônimo continua sem chamada alguma de sessão: iniciar, abandonar e concluir não tocam a API.
- [ ] Iniciar o ciclo com sessão de pé abre a sessão no servidor, e o tempo restante exibido passa a vir dos
      instantes que o servidor carimbou, não do relógio da máquina.
- [ ] Enquanto o foco corre, o heartbeat sai a cada 30s; aba em segundo plano não deixa a cadeia arrebentar
      antes dos 90s.
- [ ] Concluir o ciclo chama `complete`, e o dia mostrado passa a ser o que a API devolveu.
- [ ] Abandonar chama `abandon` e mostra o parcial que o servidor creditou — inclusive o zero de quem parou
      antes dos 5 minutos.
- [ ] `session_expired` no heartbeat encerra o ciclo na tela com mensagem traduzida, sem quebrar.
- [ ] Recarregar no meio do foco recupera a sessão por `GET /sessions/active` e volta com o tempo certo.
- [ ] Falha de rede num heartbeat não derruba o ciclo: a tela continua contando e o batimento seguinte reata.
- [ ] O dia de quem está logado vem de `GET /stats/today` — pomodoros, minutos e meta —, e o histórico local
      nunca é somado a ele.
- [ ] Ciclo cumprido offline entra na fila com `clientSessionId` próprio e drena para `/sessions/offline-batch`
      quando a conexão volta.
- [ ] Item sai da fila só depois da resposta; recusado é descartado sem retry, aceito nunca é reenviado.
- [ ] Drenar duas vezes a mesma fila credita uma vez só.
- [ ] Sair da conta descarta a fila offline junto com o resto do estado local.

### Fase 3 — Ranking, perfil e conquistas

Escopo: acúmulo semanal, view materializada, tela de ranking com posição própria, fechamento semanal com
premiação, perfil público, conquistas e opt-out total.

**Pronto quando:**

- [ ] A semana fecha às 00:00 UTC de segunda para todos, independentemente do fuso.
- [ ] Quem nunca bateu a meta na semana não aparece no ranking.
- [ ] A premiação segue a tabela e respeita o teto de 5.
- [ ] Opt-out ligado: some da lista, não pontua, não ganha freeze, perfil dá 404.
- [ ] `/u/{apelido}` renderiza streak, histórico semanal e conquistas.
- [ ] Ranking com 50 mil usuários responde em menos de 200ms (p95) no top 100.
- [ ] Reexecutar o job de fechamento da semana não duplica premiação.

---

## 6. Testes — regra inegociável do projeto

Esta seção tem precedência sobre conveniência, prazo e "é só um ajuste rápido". Nenhuma exceção é decidida por
quem está implementando.

### 6.1 Ordem: teste primeiro, sempre

Nenhuma linha de código de feature é escrita antes do teste que a descreve. O ciclo de toda feature é:

1. **Transcrever os critérios de aceite item a item**, da lista "Pronto quando" da §5 para o arquivo de teste,
   antes de pensar em qualquer código. Não de memória: lendo a lista e riscando cada linha ao cobri-la.
   Critério sem teste correspondente significa que o commit vermelho **não está pronto** — e se durante a
   implementação surgir um critério que a §5 não previa, ele é acrescentado à §5 antes de virar teste (§8.2).
   Essa etapa existe porque a falha observada foi justamente escrever os critérios na spec e depois derivar os
   testes de cabeça, deixando dois de fora sem ninguém notar até a cobertura acusar.
2. Commit `test: <feature>` com a suíte vermelha — é a prova de que a ordem foi seguida. Sem esse commit,
   não há como distinguir TDD de teste escrito depois para justificar código pronto.
3. Implementar até ficar verde, **sem tocar em nenhum teste**.
4. Commit `feat: <feature>`.
5. Refatorar com a suíte verde como rede.

Bug corrigido segue o mesmo caminho: primeiro um teste que reproduz o defeito e falha, depois a correção.

### 6.2 Testes são imutáveis durante a implementação

Enquanto uma feature está sendo implementada, **nenhum teste existente é alterado, renomeado, desabilitado ou
removido**. Se um teste falha, a hipótese padrão é que o código está errado.

Duas situações interrompem o trabalho e vão para consulta — nunca são decisão de quem implementa:

- **A regra de negócio mudou.** O teste codifica uma regra que agora é outra. Paro, apresento regra antiga ×
  regra nova, o que mais é afetado, e espero sua decisão. Se você confirmar, `docs/REGRAS-DE-NEGOCIO.md` é
  atualizado **antes** do teste.
- **O teste está mal escrito.** A regra continua válida e o teste a traduziu errado (asserção invertida, número
  trocado, setup que não representa o cenário). Paro do mesmo jeito, aponto a divergência entre o teste e o
  documento de regras, proponho a correção e espero.

**Única exceção — defeito mecânico do arcabouço.** Quando o problema não toca asserção nem regra, e sim a
mecânica de montar o teste (dublê inválido, import duplicado, tipo errado no helper, configuração do runner),
a correção é feita na hora e **listada no relatório da entrega**, sem interromper. O critério é objetivo: se
a correção muda o que o teste afirma sobre o produto, para e consulta; se muda apenas como o cenário é
construído, corrige e relata. Na dúvida sobre de que lado a correção cai, ela cai no lado de consultar.

O que chega até você nesses casos é sempre: qual teste, o que ele afirma, o que a regra diz, e a correção
proposta. Nunca um "ajustei o teste" enterrado no meio de um relatório de implementação.

### 6.3 Camadas obrigatórias antes da feature

| Camada | Onde | Precisa existir antes? | O que cobre |
|---|---|---|---|
| Domínio | `Tomatempo.Domain.Tests` | **Sim** | Apuração de minutos, streak, freezes, meta, fechamento de dia/semana, ranking, conquistas |
| Contrato de API | `Tomatempo.Api.Tests` | **Sim** | Status, payload, autorização, idempotência, rate limit — com Postgres real |
| Lógica de front | `src/web/**/*.test.ts` | **Sim** | Store do timer, fila offline, cálculo de restante, formatação |
| Componente | `src/web/**/*.test.tsx` | **Sim** | Renderização e interação do que a feature entrega |
| E2E | `src/web/e2e` | Não | Fluxos ponta a ponta; escritos depois, a partir da fase 2 |

### 6.4 Ferramentas e limites de uso

**Backend**: xUnit · **AwesomeAssertions** (fork Apache-2.0 da API do FluentAssertions 7 — mesma sintaxe
`.Should().Be()`; **FluentAssertions 8 é proibido neste repositório** por ser licença comercial) · NSubstitute ·
Testcontainers · `WebApplicationFactory` · Coverlet.

**Front-end**: Vitest · React Testing Library · `@testing-library/user-event` · `@testing-library/jest-dom` ·
MSW · fake timers do Vitest · Playwright (só E2E).

Limites que valem como regra:

- **NSubstitute só para portas externas**: relógio, envio de e-mail, provedor OAuth, geração de id. Repositório,
  `DbContext` e serviço de domínio **nunca** são dublados — teste de endpoint roda contra Postgres real via
  Testcontainers, e InMemory provider é proibido (esconde exatamente os erros de SQL que queremos pegar).
- **O domínio não recebe dublê nenhum.** É código puro: entra valor, sai valor. Se um teste de domínio precisa
  de mock, a regra vazou para fora do domínio e o desenho está errado.
- **Tempo é injetado**: `TimeProvider` em todo lugar que lê hora, `FakeTimeProvider` nos testes. É o que torna
  testável virada de dia, horário de verão, offsets de :30 e expiração de heartbeat sem `Thread.Sleep`.
- **No front, MSW no lugar de mock de `fetch`**: o teste exercita o cliente HTTP de verdade, incluindo
  serialização e tratamento de erro.
- Nada de `Thread.Sleep` ou `await delay` em teste. Tempo é sempre controlado.
- **Teste que manipula o relógio usa `fireEvent`; o resto usa `user-event`.** `user-event` e os fake timers do
  Vitest travam entre si: a promise de `user.click` nunca resolve com o relógio falso instalado — verificado em
  dez configurações (`advanceTimers`, `delay: null`, `toFake` restrito, `pointerEventsCheck: 0`, API direta),
  todas com o mesmo resultado, enquanto `fireEvent` e `act` assíncrono funcionam normalmente. Onde o tempo é
  simulado (timer, expiração, virada de dia), `fireEvent` é a escolha; onde a fidelidade da sequência de eventos
  importa de verdade — formulário, digitação, navegação por teclado, hover —, `user-event` continua obrigatório.
  Revisitar quando alguma das duas bibliotecas resolver a incompatibilidade.
- **MSW com relógio falso exige `toFake` restrito e flush explícito.** `vi.useFakeTimers()` sem argumento também
  falseia `queueMicrotask`, `setImmediate` e `process.nextTick`, e o interceptador do MSW depende deles: a
  resposta simplesmente nunca chega e o caso estoura no timeout do runner. Instalar com
  `toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date']` resolve. Pelo mesmo motivo,
  `waitFor` não serve para esperar a rede com o relógio parado — quem faz a espera é
  `await act(async () => { await vi.advanceTimersByTimeAsync(0) })`, que gira o laço de eventos uma vez e deixa
  o React repintar.

### 6.5 Convenções

- Backend: `Metodo_DeveFazerX_QuandoY`, Arrange/Act/Assert explícito, `[Theory]` para tabelas de cenário
  (as tabelas de premiação e de fusos são `[Theory]` naturais).
- Front: `describe('NomeDoComponente')` + `it('deve ...')`.
- Um comportamento por teste. Teste que verifica cinco coisas não diz qual quebrou.
- Nome do teste descreve a regra, não a implementação — sobrevive a refatoração.

### 6.6 Cobertura e CI

- **Gate de 90%** (linha e branch) em `Tomatempo.Domain` e na lógica pura do front (`src/web/src/domain/**` e
  `src/web/src/hooks/**`). Abaixo disso o CI falha.
- O resto do código é medido e reportado no PR, sem barrar merge.
- O CI também barra PR que altera código de produção sem tocar em nenhum arquivo de teste (escape possível com
  label explícita, para PRs de docs, chore e configuração).
- A suíte de domínio roda em segundos e é obrigatória no pre-commit; integração e E2E só no CI.

### 6.7 O que deliberadamente não se testa

DTO e mapeamento trivial, configuração de DI, migrations geradas, wrappers de biblioteca. Dos catálogos
estáticos testa-se só a invariante (ids únicos, contraste AA de cada preset, código de conquista sem duplicata).
Teste sem valor é passivo: custa manutenção e some no ruído quando algo real quebra.

### 6.8 Código defensivo é requisito ou não existe

`try/catch`, valor padrão, checagem de nulo e fallback "por segurança" são **comportamento**, e comportamento se
decide antes de implementar — não no meio, por instinto. A pergunta é uma só: *isto pode acontecer de verdade?*

- **Pode acontecer** → é requisito. Vira teste **antes** do código, como qualquer outra regra. Exemplos reais
  neste projeto: cota de `localStorage` estourada, formato salvo de versão anterior, navegador que lança ao
  acessar `localStorage`, resposta da API fora do contrato.
- **Não pode acontecer** → não se escreve. Fallback para caso impossível não protege nada: esconde a violação de
  invariante que deveria estourar alto, e ainda mente sobre o que o sistema tolera.

Quando o caso impossível parece precisar de defesa, o problema quase sempre é o tipo. `runningDateKey: string |
null` obrigava um `?? fallback` em três lugares; `runningDateKey: string` tornou os três desnecessários. Tipo
que descreve o estado real elimina defesa; defesa espalhada é sintoma de tipo frouxo.

**Como isso é pego:** o gate de cobertura (§6.6). Branch impossível é branch sem teste, e o gate reprova. Diante
de uma reprovação, a resposta nunca é "escrever teste até o número subir" — é decidir a que caso o branch
pertence e então testá-lo ou removê-lo.

---

## 7. Segurança

O repositório é **público desde o primeiro commit**. Isso muda a régua: um segredo commitado fica no histórico
para sempre, indexado por bots que varrem o GitHub em minutos — remover depois não desfaz o vazamento, só
esconde. Por isso as proteções da §7.5 são configuradas **antes** do primeiro `git push`, não depois.

### 7.1 Regra de processo

A cada funcionalidade nova ou correção, antes do commit de implementação, roda o checklist da §7.4. Achado
alto ou crítico bloqueia o merge — não existe "resolvo na próxima". Vulnerabilidade encontrada segue o mesmo
rito de bug da §6.1: primeiro um teste que a explora e falha, depois a correção.

### 7.2 OWASP Top 10 — controle concreto neste app

Nada aqui é genérico: cada linha diz o que existe no código do Tomatempo.

| # | Risco | Controle |
|---|---|---|
| A01 | Broken Access Control | Toda consulta filtra por `user_id` extraído do cookie de sessão. **Nenhum identificador de usuário é aceito em payload, rota ou query string.** Recurso de outro dono responde 404 (não 403 — não confirma existência). Teste de contrato obrigatório por endpoint: usuário A não lê nem altera recurso de B. |
| A02 | Cryptographic Failures | HTTPS obrigatório com HSTS; cookie `HttpOnly` + `Secure` + `SameSite=Lax`; senha com **Argon2id** (19 MiB, 2 passagens, paralelismo 1, sal de 16 bytes, saída de 32, gravado no formato PHC), com hash PBKDF2 anterior ainda aceito no login e regravado em Argon2id na hora; tokens de confirmação e reset guardados como hash, de uso único — consumir um invalida os demais pendentes do mesmo tipo —, com expiração; nenhum dado sensível em log. |
| A03 | Injection | EF Core parametrizado; SQL cru só com parâmetros — interpolação de string em query é proibida e o CI tem regra para isso. React escapa por padrão: `dangerouslySetInnerHTML` é proibido. CSP sem `unsafe-inline`. Apelido validado por whitelist `[a-z0-9_]`. |
| A04 | Insecure Design | Toda decisão de pontuação acontece no servidor com o relógio dele; tetos e limites explícitos (§3.2, §3.4); sessão única; meta e fuso só valem no dia seguinte. O antifraude é desenho, não filtro. |
| A05 | Security Misconfiguration | CORS com origem explícita — `AllowAnyOrigin` combinado com credenciais é proibido; `ProblemDetails` sem stack trace em produção; headers de segurança em **todas** as respostas, inclusive as de erro; a resposta não anuncia o servidor (sem cabeçalho `Server`); endpoint que muda estado recusa `Content-Type` de formulário, que é o único que um formulário de outro site consegue enviar; OpenAPI exposto só em dev; o container de **produção** roda como usuário não-root — o de desenvolvimento monta o código da máquina e precisa acompanhar o dono desses arquivos; `ASPNETCORE_ENVIRONMENT` nunca `Development` em produção. |
| A06 | Vulnerable Components | Dependabot ligado; `dotnet list package --vulnerable` e `npm audit` no CI barrando alta e crítica; lockfiles versionados; toda dependência nova é conferida quanto a existir mesmo e ser o pacote oficial (ver §7.3.5). |
| A07 | Identification and Auth Failures | Rate limit de 5/min em login e reset; lockout do Identity após tentativas; senha mínima de 8 caracteres verificada contra lista de senhas vazadas; e-mail confirmado exigido para pontuar; cookie regenerado no login e invalidado no logout; enumeração de conta evitada (resposta idêntica para e-mail existente ou não). |
| A08 | Software and Data Integrity | Actions do GitHub fixadas por SHA, não por tag móvel; lockfile obrigatório; nada de `curl \| bash` em build; imagens Docker por digest. |
| A09 | Logging and Monitoring Failures | Log estruturado de eventos de autenticação (cadastro, login falho, confirmação, reset, opt-out) **sem** senha, token ou e-mail completo — o endereço entra mascarado, guardando só a primeira letra e o domínio (`a***@example.com`); alerta em pico de falha de login e em erro de job. |
| A10 | SSRF | A aplicação não busca URL fornecida por usuário. Avatar é id de catálogo, jamais uma URL — e é assim justamente para não abrir essa porta. |

### 7.3 Os 5 padrões de código gerado por IA

Não existe lista oficial da Anthropic para isso. Esta é a lista **deste projeto**, montada a partir dos padrões
recorrentes documentados em pesquisa sobre código gerado por LLM e em revisão prática — e ela é revisável.
Cada item tem um controle e um teste que o pega.

1. **Autorização ausente no objeto (IDOR/BOLA)** — o endpoint autentica mas esquece de checar dono do recurso.
   *Controle*: acesso a dado de usuário só por repositório que já recebe o `userId` da sessão como parâmetro
   obrigatório; não existe método `GetById(id)` sem dono no domínio.
   *Teste*: cada endpoint autenticado tem um caso "usuário B tenta acessar recurso de A → 404".
2. **Segredo hardcoded** — connection string, chave OAuth ou signing key dentro do código ou de `appsettings.json`.
   *Controle*: §7.5, gitleaks no pre-commit e no CI.
   *Teste*: gitleaks bloqueia o commit; o CI falha se um segredo entrar por outro caminho.
3. **Confiança no cliente** — aceitar do payload um valor que decide regra (minutos, pomodoros do dia, posição,
   `goalMet`). Neste app, é literalmente o vetor de fraude do ranking.
   *Controle*: os DTOs de entrada não têm campo de resultado — só intenção. Minutos são sempre calculados pelo
   servidor a partir de `started_at` e da cadeia de heartbeats.
   *Teste*: requisição com campo extra de minutos é ignorada e a pontuação continua a do servidor.
4. **Default inseguro herdado de exemplo** — `AllowAnyOrigin` com credenciais, CSRF desligado "para o teste
   passar", stack trace em produção, cookie sem `HttpOnly`, Swagger aberto.
   *Controle*: teste de configuração que afirma os headers, as flags do cookie e a política de CORS efetivas.
   *Teste*: `Tomatempo.Api.Tests` verifica a resposta real do pipeline, não o que o código pretende.
5. **Dependência inventada ou vulnerável** — pacote que não existe (nome plausível que um atacante pode
   registrar) ou versão com CVE.
   *Controle*: toda dependência nova é confirmada no registro oficial, com autor e volume de download
   conferidos, antes de entrar no lockfile; nenhuma é adicionada "de memória".
   *Teste*: `restore` limpo no CI e gate de severidade do §7.2/A06.

### 7.4 Checklist obrigatório por mudança

Rodado ao fim de cada feature ou correção, antes do commit de implementação (uso `/security-review` sobre o diff):

- [ ] Todo endpoint novo ou alterado filtra por dono do recurso, com teste que prova.
- [ ] Nenhum identificador de usuário vem do cliente.
- [ ] Nenhum valor que decide regra de negócio vem do cliente.
- [ ] Entrada validada por whitelist (tipo, faixa, tamanho), inclusive nos lotes offline.
- [ ] Nenhuma query montada por concatenação ou interpolação.
- [ ] Nenhum segredo, token, e-mail real ou dado pessoal em código, log, teste ou fixture.
- [ ] Erro devolvido não vaza detalhe interno (stack, SQL, nome de coluna, existência de conta).
- [ ] Endpoint que muda estado exige autenticação e antiforgery, e tem rate limit adequado.
- [ ] Dependência nova: existe no registro oficial, é a oficial, está no lockfile, sem CVE alto.
- [ ] CodeQL e gitleaks verdes no PR.
- [ ] A mudança criou ou alterou regra de negócio? Se sim, os três arquivos de especificação já refletem isso (§8).

### 7.5 Segredos com o código aberto

- **Nada de segredo em arquivo versionado** — nem em código, `appsettings.json`, teste, fixture, comentário ou
  mensagem de commit. O `appsettings.json` versionado carrega apenas configuração não sensível.
- **Dev**: `dotnet user-secrets` para a API (fica fora da pasta do repositório) e `.env` para o front, ignorado
  pelo git. `.env.example` é versionado com valores obviamente falsos (`GOOGLE_CLIENT_SECRET=changeme`).
- **CI**: GitHub Secrets, sem `echo` de variável em log de workflow.
- **Produção**: variáveis de ambiente injetadas pela plataforma. Quando houver nuvem definida, migra para o cofre
  gerenciado dela sem mudar o código (a leitura já é por `IConfiguration`).
- **Barreiras**: gitleaks no pre-commit e no CI, e GitHub Secret Scanning com push protection ligado **antes do
  primeiro push**.
- **Google OAuth**: `client_secret` só existe no servidor, no fluxo authorization code. O front nunca o vê.
- **Se vazar**: rotacionar a credencial primeiro, sempre — reescrever histórico depois é higiene, não remediação.
  Todo segredo que tocou um commit público é tratado como comprometido, mesmo que o commit tenha durado 30 segundos.
- **Dados de teste**: nada real. E-mails `@example.com`, nomes fictícios, nenhum dump de produção no repositório.
- `.gitignore` cobre `.env*` (exceto `.env.example`), `*.user`, `secrets.json`, certificados, dumps e artefatos de build.

### 7.6 O que o código aberto revela — e por que tudo bem

Com o repositório público, as regras de antifraude ficam visíveis: qualquer um lê que o parcial mínimo é 5min,
que o heartbeat tolera 90s e que o teto diário é 16 sessões. Isso é aceitável porque **nenhum controle deste app
depende de sigilo** — a validação é feita pelo servidor, com o relógio do servidor, sobre dados que o cliente não
controla. Conhecer o limite não ajuda a burlá-lo.

A consequência prática é o inverso: como as regras são públicas, elas precisam estar certas. Segurança por
obscuridade não é uma opção disponível aqui, e é melhor assim.

### 7.7 Divulgação responsável e dados pessoais

- `SECURITY.md` na raiz com canal de contato e compromisso de resposta em 72h. Sem programa de recompensa.
- Dados pessoais coletados: e-mail, apelido, fuso e idioma. Nada além disso.
- Exclusão de conta remove os dados pessoais e anonimiza as linhas históricas de ranking, preservando a
  integridade das semanas já fechadas.
- Perfil público expõe exclusivamente uma whitelist de campos: apelido, avatar, streak, histórico semanal e
  conquistas. A resposta é montada por projeção explícita, nunca devolvendo a entidade inteira — e-mail e fuso
  jamais saem do servidor.

---

## 8. Documentação viva — regra de processo

Documento desatualizado é pior que documento inexistente: um avisa que você não sabe, o outro mente com
confiança. Por isso, **toda regra de negócio nova ou alterada ajusta os arquivos de especificação no mesmo
trabalho que a implementa** — nunca "depois", nunca em PR separado.

### 8.1 Quem manda em quê

| Arquivo | Papel | Linguagem |
|---|---|---|
| `docs/REGRAS-DE-NEGOCIO.md` | Fonte da verdade **do produto**. Vence qualquer outro documento em conflito. | pt-BR, sem jargão técnico |
| `docs/ESPECIFICACAO-TECNICA.md` | Como a regra vira código: modelo, algoritmo, contrato, limites. | pt-BR técnico |
| `CLAUDE.md` | Resumo denso das invariantes, carregado em toda sessão de IA. | inglês, telegráfico |

Regra derivada: se os três divergirem, `REGRAS-DE-NEGOCIO.md` está certo e os outros dois são corrigidos.

### 8.2 Ordem obrigatória

1. Regra nova ou mudança de regra é **aprovada por você** (§6.2 — nunca decido sozinho).
2. `REGRAS-DE-NEGOCIO.md` é atualizado, com a nova regra escrita em linguagem de produto.
3. `ESPECIFICACAO-TECNICA.md` é atualizado: seção 3 (algoritmo), 2 (modelo de dados) e/ou 4 (API), conforme o caso.
4. `CLAUDE.md` é atualizado se a mudança altera uma invariante que a IA precisa saber sem ler tudo.
5. Só então o teste é escrito, e só então o código.

A documentação vem **antes** do teste porque é dela que o teste é traduzido. Teste escrito a partir de uma
conversa de chat, sem passar pelo documento, é uma regra que existe só na memória da sessão — e sessões acabam.

### 8.3 O que conta como "regra de negócio"

Conta (documenta obrigatoriamente):

- Qualquer coisa que mude comportamento observável de pontuação, meta, streak, congelamento, ranking,
  elegibilidade, perfil ou conquista;
- **Qualquer número que seja regra**: 25/5/15min, parcial mínimo de 5min, 90s de tolerância de heartbeat, teto de
  16 sessões/dia, estoque máximo de 5 freezes, janela offline de 48h, faixas de premiação, 30 dias de bloqueio do
  apelido, 7 dias do marco de freeze;
- Novo estado, novo evento, nova condição de erro que o usuário percebe;
- Mudança em regra antifraude, mesmo que "só" um limite.

Não conta (não polui a documentação): refatoração, renomeação interna, ajuste de estilo, otimização de query,
melhoria de teste, mudança de biblioteca sem efeito visível.

### 8.4 Números de regra vivem em um lugar só

Todo valor da lista acima é constante nomeada em `Tomatempo.Domain` (e espelhada no módulo de domínio do front),
nunca literal espalhado pelo código. A constante carrega, em comentário, a referência à seção do documento que a
justifica. Trocar o número exige tocar: a constante, a linha do documento de regras, o teste que a exercita — os
três no mesmo commit. Se algum dos três ficar para trás, o CI ou a revisão pega.

### 8.5 Registro de decisões

`REGRAS-DE-NEGOCIO.md` mantém um log datado de decisões (a seção de 02/09/2026 é a primeira entrada). Toda
mudança de regra aprovada vira uma entrada nova, com data e o porquê — não só o quê. O "porquê" é o que impede
alguém (inclusive eu, meses depois) de reverter uma proteção antifraude achando que é excesso de zelo.

### 8.6 Verificação

- O checklist de fim de feature (§7.4) ganha um item: *a mudança alterou alguma regra? os três arquivos refletem
  isso?*
- O CI sinaliza PR que altera `src/api/**/Domain/**` sem tocar em `docs/**`, com label de exceção para os casos
  legítimos da §8.3.
- Revisão de PR trata documentação desatualizada como defeito, no mesmo nível de um teste faltando.

---

## 9. Não-funcionais

- **Segurança e privacidade**: detalhadas na §7 (controles OWASP, checklist por mudança, segredos, dados pessoais).
- **Performance**: bundle inicial < 200KB gzip; TTI < 2s em 4G.
- **Acessibilidade**: navegação por teclado completa, `aria-live` no contador, contraste AA em todos os presets,
  respeito a `prefers-reduced-motion`.
- **Observabilidade**: log estruturado, `/health` e `/health/ready`, métricas de sessões ativas e de execução dos jobs.
- **CI** (GitHub Actions): build e testes das duas pontas, lint/format (`dotnet format`, ESLint, Prettier),
  migrations aplicadas contra um Postgres efêmero, gates de cobertura da §6.6, CodeQL, gitleaks e auditoria de
  dependências da §7. PR não mergeia com CI vermelho.
- **Backup**: dump diário do Postgres quando houver ambiente de produção.

---

## 10. Ambiente de desenvolvimento

`docker compose up` sobe: `postgres:17`, `mailpit` (captura os e-mails de confirmação e reset), `api` (.NET com
hot reload) e `web` (Vite dev server). Migrations são aplicadas na subida da API em dev; em produção, como etapa
separada antes do rollout — nunca automaticamente no startup.

Variáveis: `ConnectionStrings__Db`, `Auth__Google__ClientId/Secret`, `Smtp__*`, `App__PublicUrl`.
`.env.example` versionado; `.env` fora do git.

---

## 11. Premissas assumidas (derrube se discordar)

1. **E-mail confirmado é pré-requisito para pontuar no ranking** — sem isso, criar contas descartáveis é trivial.
   Meta e streak funcionam sem confirmação.
2. **Dia coberto por freeze zera o contador do marco de 7 dias**, mantendo a streak. Sem isso, dá para farmar
   freeze usando freeze.
3. **Mudança de fuso horário só vale no dia seguinte**, como a meta.
4. **Religar o ranking após opt-out não recupera** os minutos da semana em curso.
5. **Desempate no ranking** é por quem começou a pontuar mais cedo na semana.
6. **Teto de 16 sessões apuradas por dia local**, online e offline somadas.
7. **Pausas não são persistidas** no servidor.
8. **Apelido é imutável por 30 dias** após cada troca, para evitar rodízio de identidade no ranking.

## 12. Fora de escopo (v1)

Tarefas/to-do integrado, times ou grupos privados, ranking por país, app mobile nativo, integração com
calendário, sons customizados, editor livre de cores e migração de histórico anônimo.
