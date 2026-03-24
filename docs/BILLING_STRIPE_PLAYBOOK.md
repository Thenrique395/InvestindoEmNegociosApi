# Billing Stripe - Playbook Operacional

Este documento descreve o que já foi implementado no repositório e o que ainda depende de configuração externa.

## O que já existe no código

- Checkout pago com Stripe Checkout
- Persistência local de checkout e eventos de webhook
- Ativação do plano pago somente após confirmação do pagamento
- Tratamento de estados:
  - `Pending`
  - `RequiresAction`
  - `Paid`
  - `Failed`
  - `Expired`
  - `Refunded`
  - `Cancelled`
- Portal de cobrança para renovação, método de pagamento e cancelamento
- Notificações in-app e tentativa de envio de e-mail para:
  - cobrança iniciada
  - pagamento aprovado
  - falha de cobrança

## Endpoints principais

- `POST /api/v1/billing/checkout`
- `GET /api/v1/billing/checkout-status/{checkoutId}`
- `GET /api/v1/billing/checkout-status/by-session/{sessionId}`
- `POST /api/v1/billing/portal`
- `POST /api/v1/billing/stripe/webhook`

## Variáveis obrigatórias

- `STRIPE_SECRET_KEY`
- `STRIPE_WEBHOOK_SECRET`
- `STRIPE_FRONTEND_BASE_URL`

## Variáveis opcionais

- `STRIPE_PUBLISHABLE_KEY`
- `STRIPE_PAYMENT_METHOD_TYPES`

Exemplo:

```env
STRIPE_SECRET_KEY=sk_live_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_FRONTEND_BASE_URL=https://app.seudominio.com
STRIPE_PAYMENT_METHOD_TYPES=card
```

## Métodos de pagamento

O backend aceita lista configurável via `STRIPE_PAYMENT_METHOD_TYPES`.

Exemplos:

- apenas cartão:
  - `card`
- cartão e boleto:
  - depende da disponibilidade da conta Stripe e do país
- cartão e Pix:
  - depende da disponibilidade da conta Stripe e do país

Observação importante:

- o código permite configurar os métodos
- a disponibilidade real depende da sua conta Stripe, país, moeda e recursos habilitados no painel

## Webhook

No painel Stripe, registrar um endpoint para:

- `https://SEU_BACKEND/api/v1/billing/stripe/webhook`

Eventos relevantes já tratados:

- `checkout.session.completed`
- `checkout.session.async_payment_succeeded`
- `checkout.session.async_payment_failed`
- `checkout.session.expired`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`
- `invoice.paid`
- `invoice.payment_failed`
- `charge.refunded`

## Teste local com Stripe CLI

Exemplo:

```bash
stripe listen --forward-to http://localhost:5059/api/v1/billing/stripe/webhook
```

Depois copiar o `whsec_...` retornado e usar em:

```env
STRIPE_WEBHOOK_SECRET=whsec_...
```

## Fluxo esperado

1. Usuário autenticado inicia `POST /billing/checkout`
2. Backend cria checkout local e Stripe Checkout Session
3. Frontend redireciona para a URL do Stripe
4. Stripe retorna o usuário ao frontend
5. Webhook confirma o status real
6. Backend ativa ou bloqueia o plano conforme o evento recebido
7. Área de assinatura usa portal de cobrança para gestão posterior

## O que ainda depende de atuação manual

- criar e configurar a conta Stripe real
- habilitar os métodos de pagamento desejados no painel
- configurar domínio público do frontend/backend
- registrar o webhook no ambiente correto
- preencher os secrets/vars no GitHub Actions e/ou VPS
- validar compras reais em sandbox antes de produção
- definir política comercial final:
  - reembolso
  - prazo de cancelamento
  - retry de cobrança
  - inadimplência e downgrade operacional

## Checklist de ativação

- [ ] `STRIPE_SECRET_KEY` configurada
- [ ] `STRIPE_WEBHOOK_SECRET` configurada
- [ ] `STRIPE_FRONTEND_BASE_URL` configurada
- [ ] webhook criado no painel Stripe
- [ ] ambiente publicado com backend acessível externamente
- [ ] compra de teste validada até o plano ficar ativo
- [ ] portal de cobrança validado
- [ ] cancelamento e falha de pagamento validados em sandbox
