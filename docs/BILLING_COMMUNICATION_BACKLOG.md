# Billing Communication Backlog

Backlog técnico para implementar as comunicações de billing (e-mail e in-app) por evento de
assinatura.

A direção de produto (recorrência, janela de recuperação de 7 dias, downgrade, reativação e
tom acolhedor/comercial) já está consolidada e não deve ser repetida aqui — ver
[../../docs/DECISIONS/2026-03-24-commercial-offer-and-billing-rules.md](../../docs/DECISIONS/2026-03-24-commercial-offer-and-billing-rules.md)
(seção "Comunicação") e [../../docs/BUSINESS_RULES.md](../../docs/BUSINESS_RULES.md).

Este documento cobre o estado de implementação das comunicações e os textos finais aprovados.

## Status por evento

| # | Evento | Status | Observação |
|---|---|---|---|
| 1 | Compra aprovada | ✅ Implementado | `NotifyApprovedAsync` — assunto: "Bem-vindo ao plano {Nome}!"; CTA: "Acessar o app" → `/dashboard` |
| 2 | Renovação aprovada | ✅ Implementado | `NotifyRenewalApprovedAsync` — assunto: "Assinatura do plano {Nome} renovada"; sem CTA (notificação discreta) |
| 3 | Falha de pagamento | ✅ Implementado | `NotifyFailedAsync` — assunto: "Problema na confirmação do pagamento" (primeira compra) ou "Problema ao renovar sua assinatura" (renovação); CTA: "Atualizar pagamento" → `/assinatura` |
| 4 | Lembrete de janela de recuperação | ✅ Implementado | `NotifyGracePeriodReminderAsync` — assunto: "Seu acesso premium está em risco"; CTA: "Atualizar pagamento" → `/assinatura` |
| 5 | Downgrade para `basic` | ✅ Implementado | `NotifyDowngradedAsync` — assunto: "Acesso premium encerrado"; CTA: "Reativar plano" → `/assinatura` |
| 6 | Pagamento regularizado | ✅ Implementado | `NotifyReactivatedAsync` — assunto: "Seu acesso premium está restaurado"; CTA: "Acessar o app" → `/dashboard` |

## Copy final por evento

### 1. Compra aprovada

- Assunto: `Bem-vindo ao plano {Nome}!`
- Título: `Plano {Nome} ativado`
- Corpo: `Ótimo! Seu pagamento foi aprovado e o plano {Nome} já está disponível na sua conta. Acesse o app e aproveite todos os recursos.`
- CTA: `Acessar o app` → `{frontendBase}/dashboard`

### 2. Renovação aprovada

- Assunto: `Assinatura do plano {Nome} renovada`
- Título: `Assinatura renovada`
- Corpo: `Tudo certo! Sua assinatura do plano {Nome} foi renovada automaticamente. Nenhuma ação necessária.`
- CTA: nenhum (notificação discreta)

### 3. Falha de pagamento (primeira compra)

- Assunto: `Problema na confirmação do pagamento`
- Título: `Falha na cobrança`
- Corpo: `A cobrança não foi aprovada. Atualize sua forma de pagamento para continuar com acesso ao plano.`
- CTA: `Atualizar pagamento` → `{frontendBase}/assinatura`

### 3b. Falha de pagamento (renovação — `checkout.Status == Paid`)

- Assunto: `Problema ao renovar sua assinatura`
- Título: `Falha na cobrança`
- Corpo: `Tivemos um problema ao renovar sua assinatura. Atualize sua forma de pagamento para continuar com todos os recursos premium.`
- CTA: `Atualizar pagamento` → `{frontendBase}/assinatura`

### 4. Lembrete de janela de recuperação

- Assunto: `Seu acesso premium está em risco`
- Título: `Acesso premium em risco`
- Corpo: `Seu acesso premium está quase sendo interrompido. Regularize o pagamento até {data} para continuar usando seu plano sem perder o ritmo.`
- CTA: `Atualizar pagamento` → `{frontendBase}/assinatura`

### 5. Downgrade para `basic`

- Assunto: `Acesso premium encerrado`
- Título: `Plano atualizado para Essencial`
- Corpo: `Como não conseguimos confirmar o pagamento, seu acesso voltou para o plano Essencial. Você pode reativar seu plano premium a qualquer momento.`
- CTA: `Reativar plano` → `{frontendBase}/assinatura`

### 6. Pagamento regularizado

- Assunto: `Seu acesso premium está restaurado`
- Título: `Plano reativado`
- Corpo: `Tudo certo novamente. Seu pagamento foi confirmado e seu plano premium já está ativo.`
- CTA: `Acessar o app` → `{frontendBase}/dashboard`

## Dados disponíveis nos templates

- nome do usuário (`user.Name`)
- nome amigável do plano (`PlanDisplayName(checkout.PlanCode)`)
- CTA via `StripeOptions.FrontendBaseUrl` (configurado por ambiente)
- data limite da janela de graça (passada para `NotifyGracePeriodReminderAsync`)

## O que não deve entrar em implementação

- tom agressivo, jurídico ou ameaçador
- mensagem vaga sem CTA claro
- trial ou comunicação que gere interpretação confusa do estado da assinatura
- link que não leve diretamente para atualização da cobrança quando esse for o objetivo

## Critérios de aceite

- cada evento relevante de billing gera a comunicação correta no canal correto
- não há duplicação indevida de mensagens (idempotência via `referenceKey`)
- o usuário consegue sair da mensagem e chegar ao fluxo certo de regularização
- downgrade e reativação ficam claros para o usuário
- o tom final permanece acolhedor/comercial
