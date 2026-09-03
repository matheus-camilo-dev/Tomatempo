# Política de segurança — Tomatempo

## Reportar uma vulnerabilidade

Se você encontrou uma vulnerabilidade neste projeto, por favor **não abra uma issue pública**.

Use o [Private vulnerability reporting](https://docs.github.com/code-security/security-advisories/guidance-on-reporting-and-writing/privately-reporting-a-security-vulnerability)
do GitHub, na aba **Security** deste repositório.

- Resposta inicial: até **72 horas**.
- Correção: prazo combinado caso a caso, conforme severidade.
- Divulgação: coordenada, após a correção estar publicada.

Não há programa de recompensa financeira. Crédito público ao relator é dado com prazer, se desejado.

## Escopo

Em escopo: código deste repositório e a instância oficial do Tomatempo, quando existir.

Fora de escopo: ataques de negação de serviço, engenharia social, spam de formulários, e achados
automatizados sem prova de impacto real.

## O que este projeto assume

O código é aberto, e as regras de antifraude são públicas por consequência (limites de tempo, tetos diários,
tolerâncias). Isso é intencional: **nenhum controle depende de sigilo**. Toda pontuação é validada no servidor,
com o relógio do servidor, sobre dados que o cliente não controla.

Se você encontrar um controle cuja segurança dependa de alguém não conhecer seu funcionamento, isso é um bug —
e queremos saber.

## Segredos

Nenhuma credencial deve existir em código, configuração versionada, teste ou fixture. Se você identificar um
segredo exposto no histórico, reporte pelo canal acima com urgência: ele será rotacionado antes de qualquer
limpeza de histórico.
