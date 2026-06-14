# Billing Communication Backlog

Backlog técnico para implementar as comunicações de billing (e-mail e in-app) por evento de
assinatura.

A direção de produto (recorrência, janela de recuperação de 7 dias, downgrade, reativação e
tom acolhedor/comercial) já está consolidada e não deve ser repetida aqui — ver
[../../docs/DECISIONS/2026-03-24-commercial-offer-and-billing-rules.md](../../docs/DECISIONS/2026-03-24-commercial-offer-and-billing-rules.md)
(seção "Comunicação") e [../../docs/BUSINESS_RULES.md](../../docs/BUSINESS_RULES.md).

Este documento cobre apenas o que falta implementar tecnicamente.

## Status por evento

| # | Evento | Status | Observação |
|---|---|---|---|
| 1 | Compra aprovada | ✅ Implementado | `BillingNotificationService.NotifyApprovedAsync` + `NotificationKind.BillingApproved` (e-mail + in-app) |
| 2 | Renovação aprovada | ❌ Não implementado | sem `NotificationKind` ou template específico |
| 3 | Primeira falha de renovação | ⚠️ Parcial | `NotifyFailedAsync` + `NotificationKind.BillingFailed` existem de forma genérica, mas sem CTA específico de "atualizar forma de pagamento" e sem distinção entre falha de checkout inicial e falha de renovação |
| 4 | Lembrete antes do fim da janela de recuperação | ❌ Não implementado | depende do enforcement da janela de 7 dias (`ROADMAP.md` 2.3/3.1) |
| 5 | Downgrade para `basic` | ❌ Não implementado | depende do mesmo enforcement |
| 6 | Pagamento regularizado | ❌ Não implementado | sem `NotificationKind` ou template |

## Detalhe por evento pendente

### 2. Renovação aprovada

- Objetivo: confirmar renovação sem gerar ruído excessivo.
- Canais: e-mail discreto ou recibo.
- CTA principal: abrir portal de cobrança ou área da assinatura, se fizer sentido.
- Estado esperado: assinatura continua ativa.

### 3. Primeira falha de renovação (completar)

- Adicionar CTA específico "atualizar cartão ou forma de pagamento" ao template existente.
- Diferenciar, no payload/`NotificationKind`, falha de checkout inicial de falha de
  renovação recorrente.
- Estado esperado: assinatura em recuperação de cobrança.

### 4. Lembrete antes do fim da janela

- Objetivo: reforçar urgência antes da suspensão do premium.
- Canais: e-mail + notificação in-app.
- CTA principal: atualizar cartão ou forma de pagamento.
- Estado esperado: assinatura ainda em recuperação, próxima da suspensão.
- Pré-requisito: enforcement real da janela de 7 dias (`ROADMAP.md` 2.3/3.1).

### 5. Downgrade para `basic`

- Objetivo: informar que o acesso premium foi suspenso por falta de regularização.
- Canais: e-mail.
- CTA principal: reativar plano premium.
- Estado esperado: assinatura rebaixada para `basic` (ou premium suspenso, conforme
  implementação final do enforcement).

### 6. Pagamento regularizado

- Objetivo: confirmar retorno do premium.
- Canais: e-mail + opcionalmente notificação in-app.
- CTA principal: voltar ao produto.
- Estado esperado: assinatura premium reativada.

## Backlog técnico sugerido

### Backend

- mapear eventos internos e webhooks Stripe que disparam cada comunicação pendente (2, 4, 5, 6)
- estender `BillingNotificationService` com os `NotificationKind` faltantes
- persistir status relevante para evitar envios duplicados
- expor URL segura para atualização de pagamento
- registrar auditoria mínima de envio

### Frontend

- criar banners/notificações in-app por estado de cobrança (lembrete, downgrade, reativação)
- criar telas ou pontos de entrada claros para atualização de pagamento
- mostrar downgrade e reativação sem ambiguidade

### Templates de e-mail pendentes

- renovação aprovada (evento 2)
- lembrete final (evento 4)
- downgrade para `basic` (evento 5)
- reativação (evento 6)
- completar o template de primeira falha (evento 3) com o CTA de atualização de pagamento

## Dados mínimos por template

- nome do usuário
- nome do plano
- status atual da assinatura
- data da próxima tentativa ou da suspensão, quando aplicável
- CTA principal
- link do portal ou da atualização de pagamento

## Exemplos de copy aprovados como direção

### Falha de cobrança

- "Tivemos um problema ao renovar sua assinatura. Atualize sua forma de pagamento para
  continuar com todos os recursos premium."

### Lembrete

- "Seu acesso premium está quase sendo interrompido. Regularize o pagamento e continue
  usando seu plano sem perder o ritmo."

### Downgrade para `basic`

- "Como não conseguimos confirmar o pagamento, seu acesso voltou para o plano BASIC. Você
  pode reativar seu plano premium a qualquer momento."

### Reativação

- "Tudo certo novamente. Seu pagamento foi confirmado e seu plano premium já está ativo."

## O que não deve entrar em implementação

- tom agressivo, jurídico ou ameaçador
- mensagem vaga sem CTA claro
- trial ou comunicação que gere interpretação confusa do estado da assinatura
- link que não leve diretamente para atualização da cobrança quando esse for o objetivo

## Critérios de aceite

- cada evento relevante de billing gera a comunicação correta no canal correto
- não há duplicação indevida de mensagens
- o usuário consegue sair da mensagem e chegar ao fluxo certo de regularização
- downgrade e reativação ficam claros para o usuário
- o tom final permanece acolhedor/comercial
