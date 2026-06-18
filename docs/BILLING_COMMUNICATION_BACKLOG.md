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
| 2 | Renovação aprovada | ✅ Implementado | `NotifyRenewalApprovedAsync` + `NotificationKind.BillingRenewalApproved`; acionado em `SyncAsync` quando `previousStatus == Active` |
| 3 | Primeira falha de renovação | ✅ Implementado | `NotifyFailedAsync` com copy contextual: quando `checkout.Status == Paid` (renovação), usa "Atualize sua forma de pagamento"; caso contrário, usa copy genérico de falha de checkout |
| 4 | Lembrete antes do fim da janela de recuperação | ✅ Implementado | `NotifyGracePeriodReminderAsync` + `NotificationKind.BillingGracePeriodReminder`; acionado pelo `RoboExpiracaoAssinaturas` quando `RenewsAt` está nas últimas 24h da janela de graça |
| 5 | Downgrade para `basic` | ✅ Implementado | `NotifyDowngradedAsync` + `NotificationKind.BillingDowngraded`; acionado pelo `RoboExpiracaoAssinaturas` após `MarkExpired` |
| 6 | Pagamento regularizado | ✅ Implementado | `NotifyReactivatedAsync` + `NotificationKind.BillingReactivated`; acionado em `SyncAsync` quando `previousStatus == PastDue` e Stripe confirma pagamento |

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
