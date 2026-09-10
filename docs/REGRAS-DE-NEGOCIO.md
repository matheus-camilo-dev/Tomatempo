# Site Pomodoro — Regras de Negócio

> Fonte da verdade do produto, consolidada na sessão de descoberta de 28/08/2026.

## Visão geral

Site web de Pomodoro utilizável **anonimamente**, com **login opcional** que desbloqueia: meta diária, sequência de dias (streak) com congelamentos, ranking global semanal e estatísticas pessoais.

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
- **Senha**: mínimo de 8 caracteres, e senha muito comum (as que aparecem em toda lista de vazamento) é
  recusada — no cadastro e na troca.
- **O site nunca revela se um e-mail tem conta**: cadastro, login e recuperação dizem a mesma coisa para
  e-mail conhecido e desconhecido.
- **Excluir a conta** apaga os dados pessoais e libera o e-mail e o apelido para um cadastro novo. Quem tem
  senha precisa digitá-la para confirmar — sessão esquecida aberta não apaga conta de ninguém.
- **Conta começa do zero** — o histórico anônimo do navegador não migra (proteção do ranking contra dados forjados).
- **O placar de quem está logado é o da conta**, servido pelo servidor: o histórico guardado no navegador
  continua sendo o do anônimo e nunca é somado ao da conta.
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

- Claro, escuro e "seguir o sistema", para todos — sem editor livre de cores e sem galeria de temas.
- Para quem tem conta, a escolha acompanha a conta, junto com idioma e fuso, e não só o navegador.

## 7. Estatísticas pessoais (logados)

- Minutos por dia/semana, histórico de streak, melhor semana. Alimentam o perfil público.
- O dia que a pessoa logada vê — pomodoros, minutos e meta — é sempre o que o servidor apurou, e não uma
  contagem feita no navegador.

## 8. PWA / Offline

- Site **instalável** (ícone próprio, janela de app, notificações melhores).
- **Offline**: aviso visível de que está offline. O timer continua funcionando e os pomodoros **contam para a meta diária e a streak**, mas **não pontuam no ranking** — minutos de ranking exigem estar online, com validação do servidor.
- **O que foi feito offline fica numa fila no navegador** e sobe para a conta quando a conexão volta. A fila só
  recolhe ciclo de quem está logado: o que o anônimo faz continua sendo dele e não migra.
- **Sair da conta descarta o que ainda estiver na fila.** Quem sai offline com ciclos pendentes perde esses
  minutos. **A sessão que cai sozinha** — cookie expirado ou revogado — vale como saída: a fila é descartada
  do mesmo jeito, e **entrar numa conta também começa com a fila vazia**.

## 9. Antifraude (resumo)

1. Minutos de ranking validados no servidor (início/fim reportados).
2. Sem emenda automática de ciclos (presença obrigatória entre pomodoros).
3. Parcial mínimo de 5 minutos.
4. Meta só muda para o dia seguinte.
5. Conta nova começa zerada.
6. Offline não pontua no ranking.
7. Fila offline descartada ao sair, ao entrar e na queda de sessão (não atravessa de uma conta para outra).

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

### 09/09/2026 — o timer logado conversando com o servidor

- **O dia de quem está logado vem do servidor, e o histórico local não é somado a ele.** *Por quê*: com duas
  contagens na mesma tela, a divergência entre elas vira suporte — e a contagem que o navegador faz é
  justamente a que uma pessoa mal-intencionada consegue editar. Uma fonte de verdade só, e é a que já decide
  meta, streak e ranking. O histórico local continua existindo para o anônimo, que não tem servidor algum.
- **Sair da conta descarta o que ainda estiver na fila offline.** *Por quê*: nenhum identificador de usuário
  pode ser guardado no navegador, então a fila não tem como saber de quem ela é. Sobrevivendo ao logout, ela
  drenaria na próxima conta que entrasse naquele navegador — creditar o foco de uma pessoa na conta de outra,
  exatamente o dano que "conta nova começa zerada" existe para impedir. Custo assumido: quem sai offline com
  ciclos pendentes perde esses minutos; é o lado barato do erro.

### 10/09/2026 — escopo: temas e estatísticas

- **A galeria de temas sai do produto.** Claro, escuro e "seguir o sistema" continuam, para todo mundo.
  *Por quê*: era a única funcionalidade paga em complexidade — catálogo versionado, contraste AA por preset,
  campo próprio no banco — sem ligação alguma com o que o produto faz. Nada do que já está no ar depende dela.
  A coluna `theme_preset_id` fica no banco, reservada e sem uso, para não custar uma migração agora.
- **Estatísticas pessoais vão para a fase 3.** *Por quê*: a série histórica nasce junto do perfil público e do
  acúmulo semanal, que leem os mesmos dados; construir as duas coisas juntas evita fazer a mesma consulta duas
  vezes. O dia corrente, que o timer precisa, já está entregue.

### 10/09/2026 — recuperar o ciclo ao voltar para a tela

- **Abrir a tela com um foco em andamento retoma o ciclo**, com o tempo que o servidor diz — mesmo que o
  navegador não tenha mais nada guardado, e mesmo em outro dispositivo. *Por quê*: enquanto o servidor
  considera a pessoa em foco, ela precisa ver o mesmo. Sem isso, quem perdeu o estado local fica olhando um
  timer parado e perde os minutos de um ciclo que, para o servidor, estava correndo.
- **Ler a sessão não a toma de ninguém.** *Por quê*: quem assume o foco de outra aba é começar um ciclo, não
  abrir a tela. Se abrir bastasse, uma segunda aba aberta por engano derrubaria a sessão de quem está
  trabalhando.
- **Servidor sem sessão ativa não interrompe o ciclo que está na tela**: ele passa a valer como ciclo cumprido
  sem servidor e vai para a fila. *Por quê*: "não há sessão" tanto pode ser sessão já apurada quanto ciclo que
  nasceu offline, e não dá para distinguir os dois pelo navegador. Mandar para a fila acerta nos dois: a
  sessão já apurada faz o item ser recusado por sobreposição, e o ciclo que nasceu offline é creditado. O
  aviso de sessão perdida fica reservado para quando o servidor afirma que a sessão saiu desta aba.

### 10/09/2026 — a fila offline no navegador

- **A sessão que cai sozinha descarta a fila, igual ao logout.** *Por quê*: o motivo do descarte nunca foi o
  clique em "sair", e sim a fila não saber de quem ela é. Cookie expirado ou revogado deixa o navegador
  exatamente no mesmo estado do logout, e a próxima conta a entrar ali receberia o foco de outra pessoa. Custo
  assumido: a fila pendente no instante em que a sessão vence se perde — janela pequena, porque com a conexão
  de pé ela já teria drenado.
- **Ciclo cumprido sem sessão no servidor é o que entra na fila**, seja o pomodoro inteiro ou o parcial de quem
  abandonou. *Por quê*: a fila existe para o que o servidor não viu acontecer. Ciclo que abriu sessão e não
  conseguiu fechá-la não entra — quem o encerra é o varredor, e enfileirá-lo também o contaria duas vezes.
- **Parcial abaixo de 5 minutos não entra na fila.** *Por quê*: é a mesma regra do parcial mínimo já valendo
  antes de a rede voltar; enfileirar para o servidor recusar depois só gasta viagem.
- **Entrar numa conta começa com a fila vazia.** *Por quê*: sair não é o único jeito de a sessão acabar — quem
  fecha a aba e não volta deixa a fila para trás sem nunca ter deslogado, e a próxima pessoa a entrar naquele
  navegador receberia o foco dela. Como a fila não pode saber de quem ela é (nenhum identificador de usuário
  no navegador), o único ponto seguro de decidir isso é a entrada. Custo assumido: quem volta a entrar na
  própria conta sem ter saído perde os ciclos pendentes — o mesmo custo já aceito na queda de sessão.
- **Navegador sem armazenamento para a fila não impede ninguém de usar o timer.** *Por quê*: o ciclo nunca
  dependeu de conta nem de rede, e não vai passar a depender de banco no navegador. Quem está em navegação
  privada com o armazenamento bloqueado perde os minutos offline, não o pomodoro.

## Pendências

- Nenhuma pendência de produto em aberto. Premissas técnicas assumidas e passíveis de revisão estão listadas na
  seção 8 de `docs/ESPECIFICACAO-TECNICA.md`.
