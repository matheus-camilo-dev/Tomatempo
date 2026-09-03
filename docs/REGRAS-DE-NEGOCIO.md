# Site Pomodoro — Regras de Negócio

> Fonte da verdade do produto, consolidada na sessão de descoberta de 28/08/2026.

## Visão geral

Site web de Pomodoro utilizável **anonimamente**, com **login opcional** que desbloqueia: galeria de temas de cores, meta diária, sequência de dias (streak) com congelamentos e ranking global semanal.

## 1. Timer

- **Fixo para todos**: 25min de foco / 5min de pausa curta / 15min de pausa longa a cada 4 ciclos. Sem configuração de durações.
- **Confirmação de presença**: o timer nunca emenda ciclos sozinho — ao fim de cada pomodoro o usuário precisa interagir para iniciar a pausa ou o próximo ciclo.
- **Tempo parcial conta**: abandonar um pomodoro no minuto 20 rende 20 minutos no placar de minutos de foco. Parciais abaixo de **5 minutos não contam**.
- **Sons e notificações**: alarme ao fim do ciclo e notificação do navegador quando a aba está em segundo plano.

## 2. Anônimo vs. Logado

| Funcionalidade | Anônimo | Logado |
|---|---|---|
| Timer completo | ✔ | ✔ |
| Tema claro/escuro | ✔ | ✔ |
| Galeria de temas (presets) | — | ✔ |
| Meta diária | — | ✔ |
| Streak + congelamentos | — | ✔ |
| Ranking semanal | — | ✔ |
| Estatísticas pessoais | — | ✔ |

- **Login**: e-mail/senha e social (Google).
- **Entrar com Google em um e-mail que já tem conta de senha** leva para a conta existente, sem criar uma
  segunda. Exige que o próprio Google afirme que o e-mail é confirmado; se não afirmar, o acesso é recusado.
- **Quem se cadastra pelo Google passa por uma tela de boas-vindas** para escolher apelido, meta e fuso.
  Enquanto não escolher, a conta existe mas não pontua.
- **Confirmação de e-mail**: o link vale **24 horas** e serve **uma vez só**. O cadastro já entra logado —
  meta e streak funcionam antes de confirmar; o **ranking não**.
- **Recuperação de senha**: o link vale **1 hora** e serve **uma vez só**.
- **O site nunca revela se um e-mail tem conta**: cadastro, login e recuperação dizem a mesma coisa para
  e-mail conhecido e desconhecido.
- **Conta começa do zero** — o histórico anônimo do navegador não migra (proteção do ranking contra dados forjados).
- **CTA para deslogados**: área na tela convidando a criar conta para participar do ranking.

## 3. Meta diária

- Unidade: **número de pomodoros completos** por dia.
- Alterações na meta **só valem a partir do dia seguinte** (impede reduzir a meta no fim do dia para salvar a streak).
- O "dia" segue o **fuso horário local** do usuário.

## 4. Streak (sequência de dias)

- A sequência continua quando o usuário **bate a meta diária**.
- **Congelamentos (freezes)**: se o dia termina sem a meta batida, um congelamento é **consumido automaticamente** e a sequência sobrevive.
- Congelamentos **acumulam entre semanas com teto de estoque de 5**.

## 5. Ranking global semanal

- **Métrica**: minutos de foco acumulados na semana (inclui parciais ≥ 5min).
- **Elegibilidade**: ter batido a meta diária **ao menos 1 vez** naquela semana.
- **Semana fecha em UTC** (instante único global); o dia da meta/streak é local.
- **Premiação em congelamentos** ao fechar a semana (máximo 3/semana):

  | Colocação | Congelamentos |
  |---|---|
  | 1º–20º | 3 |
  | 21º–50º | 2 |
  | 51º–100º | 1 |
  | Abaixo de 100º | 0 |

- **Perfil rico**: apelido + avatar no ranking; página pública de perfil com streak atual, histórico de semanas e conquistas.

## 6. Temas de cores

- Todos os usuários: claro/escuro.
- Logados: galeria de **temas prontos (presets)** mantidos pelo produto — sem editor livre de cores.

## 7. Estatísticas pessoais (logados)

- Minutos por dia/semana, histórico de streak, melhor semana. Alimentam o perfil público.

## 8. PWA / Offline

- Site **instalável** (ícone próprio, janela de app, notificações melhores).
- **Offline**: aviso visível de que está offline. O timer continua funcionando e os pomodoros **contam para a meta diária e a streak**, mas **não pontuam no ranking** — minutos de ranking exigem estar online, com validação do servidor.

## 9. Antifraude (resumo)

1. Minutos de ranking validados no servidor (início/fim reportados).
2. Sem emenda automática de ciclos (presença obrigatória entre pomodoros).
3. Parcial mínimo de 5 minutos.
4. Meta só muda para o dia seguinte.
5. Conta nova começa zerada.
6. Offline não pontua no ranking.

## 10. Log de decisões

> Toda regra nova ou alterada entra aqui como uma entrada datada, registrando **o quê e o porquê**. O "porquê" é
> o que impede que uma proteção antifraude seja revertida no futuro por parecer excesso de zelo.
> A ordem de atualização quando uma regra muda está em `docs/ESPECIFICACAO-TECNICA.md` §8.

### 02/09/2026 — fechamento das pendências de descoberta

- **Nome do produto**: Tomatempo.
- **Meta diária**: perguntada no cadastro (2 / 4 / 6 / 8, com 4 pré-selecionado).
- **Congelamentos**: 2 na conta nova, +1 a cada 7 dias consecutivos de meta batida e a premiação semanal do
  ranking; teto de estoque continua 5. A tela de congelamentos informa a origem de cada crédito.
- **Dia sem nenhuma atividade** também consome congelamento (a sequência sobrevive enquanto houver estoque).
- **Privacidade no ranking**: opt-out **total** — quem desliga não pontua, não vê a própria posição, não ganha
  congelamento por ranking e não tem perfil público.
- **Conquistas**: quatro famílias — marcos de streak, volume de foco, desempenho no ranking e hábito/consistência.
- **Idiomas**: pt-BR e inglês desde o lançamento.
- **Perfil**: apelido único (vira a URL) e avatar escolhido em galeria curada, sem upload de imagem.
- **Validação antifraude**: início/fim com heartbeat a cada ~30s e uma única sessão de foco ativa por usuário.
- **Offline**: sincroniza ao reconectar, aceito apenas dentro de 48h, sem sobreposição e com teto diário.
- **Stack e hospedagem**: React + API .NET, PostgreSQL, Docker; nuvem definida depois (ver
  `docs/ESPECIFICACAO-TECNICA.md`).

### 03/09/2026 — conta e acesso (abertura da fase 2)

- **Google vincula à conta que já existe** quando o e-mail já tem cadastro de senha e o próprio Google o dá
  como confirmado. *Por quê*: duas contas para a mesma pessoa duplicariam streak e minutos de ranking — o
  mesmo dano que a regra "conta nova começa zerada" existe para evitar. Exigir o e-mail confirmado pelo Google
  é o que impede alguém de tomar conta alheia apenas alegando ser dono daquele endereço.
- **Cadastro por Google exige a tela de boas-vindas** (apelido, meta e fuso) antes de a conta pontuar.
  *Por quê*: o apelido vira a URL pública do perfil e fica travado por 30 dias; um apelido gerado pelo servidor
  a partir do e-mail seria uma identidade pública que o usuário não escolheu e não poderia trocar em seguida.
- **O cadastro já entra logado, com o e-mail ainda não confirmado.** *Por quê*: meta e streak nunca dependeram
  da confirmação (só o ranking depende), e mandar o usuário para a caixa de entrada antes do primeiro pomodoro
  o perde justamente no minuto em que ele está mais disposto a começar.
- **Link de confirmação vale 24h; link de reset de senha vale 1h**, ambos de uso único. *Por quê*: a
  confirmação disputa espaço com uma caixa de entrada que pode levar horas para ser lida; o reset é a chave da
  conta, e cada minuto de validade é tempo em que um e-mail vazado dá acesso.
- **Nenhuma resposta revela se um e-mail tem conta.** *Por quê*: saber quem usa o site é dado pessoal, e
  enumerar contas é o primeiro passo de qualquer ataque de senha. Resíduo assumido: como o cadastro
  bem-sucedido já devolve a sessão, quem observa a resposta crua ainda distingue e-mail novo de e-mail
  existente pela presença do cookie. Contido pelo limite de 5 tentativas por minuto, o mesmo do login;
  eliminá-lo exigiria abrir mão do login automático no cadastro.

## Pendências

- Nenhuma pendência de produto em aberto. Premissas técnicas assumidas e passíveis de revisão estão listadas na
  seção 8 de `docs/ESPECIFICACAO-TECNICA.md`.
