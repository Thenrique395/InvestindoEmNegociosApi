# Billing Stripe — Playbook Operacional

Playbook interno da integração Stripe deste projeto.

Este documento não substitui a documentação oficial da Stripe. Ele existe para adaptar o uso oficial ao estado atual do código.

## Fontes oficiais

- subscriptions com Checkout: https://docs.stripe.com/payments/subscriptions
- webhooks para subscriptions: https://docs.stripe.com/billing/subscriptions/webhooks
- customer portal: https://docs.stripe.com/customer-management
- webhooks gerais: https://docs.stripe.com/webhooks
- reprocessamento de webhooks não entregues: https://docs.stripe.com/webhooks/process-undelivered-events

## O que a Stripe recomenda

Para o cenário de assinatura com Checkout, a Stripe recomenda:

- criar produtos e preços no painel ou via API
- usar `Checkout Session` com `mode=subscription`
- passar `line_items.price` com um `Price` previamente criado
- usar webhook como fonte final do estado da assinatura
- usar Customer Portal para gestão posterior da assinatura

Leitura prática:

- retorno do browser não é confirmação final de pagamento
- o webhook continua sendo a confirmação real
- reuso de `Customer` evita duplicidade de clientes na Stripe

## Como o projeto implementa hoje

O backend já implementa:

- `POST /api/v1/billing/checkout`
- `GET /api/v1/billing/checkout-status/{checkoutId}`
- `GET /api/v1/billing/checkout-status/by-session/{sessionId}`
- `POST /api/v1/billing/portal`
- `POST /api/v1/billing/stripe/webhook`

Estados locais tratados:

- `Pending`
- `RequiresAction`
- `Paid`
- `Failed`
- `Expired`
- `Refunded`
- `Cancelled`

Eventos Stripe tratados:

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

## Alinhamento atual com a Stripe oficial

O código atual segue a direção oficial nos pontos mais importantes:

- usa Checkout em `mode=subscription`
- usa webhook assinado para confirmação
- usa portal de cobrança para gestão posterior
- reaproveita `Customer` existente quando a assinatura local já possui `ExternalCustomerId`

Também foi ajustado nesta fase para:

- preferir `Price IDs` configuráveis, alinhando o fluxo ao padrão oficial da Stripe
- manter fallback para `price_data` inline apenas quando os `Price IDs` ainda não estiverem configurados

Regra prática:

- em produção, o recomendado é usar `Price IDs`
- o fallback dinâmico existe para transição e ambiente de desenvolvimento, não como padrão final desejado

## Configuração obrigatória

Variáveis mínimas:

- `STRIPE_SECRET_KEY`
- `STRIPE_WEBHOOK_SECRET`
- `STRIPE_FRONTEND_BASE_URL`

## Configuração recomendada

Além do mínimo, o ideal é configurar:

- `Stripe:PriceIds:intermediate.monthly`
- `Stripe:PriceIds:intermediate.yearly`
- `Stripe:PriceIds:advanced.monthly`
- `Stripe:PriceIds:advanced.yearly`

Exemplo em `appsettings`:

```json
{
  "Stripe": {
    "SecretKey": "sk_live_...",
    "WebhookSecret": "whsec_...",
    "FrontendBaseUrl": "https://app.seudominio.com",
    "PaymentMethodTypes": [ "card" ],
    "PriceIds": {
      "intermediate.monthly": "price_123",
      "intermediate.yearly": "price_456",
      "advanced.monthly": "price_789",
      "advanced.yearly": "price_abc"
    }
  }
}
```

Observação:

- hoje o mapeamento de `PriceIds` está pensado para configuração por JSON/config provider
- se quiser padronizar isso também via env, a estratégia de configuração precisa ser expandida conscientemente

## Métodos de pagamento

O backend aceita `PaymentMethodTypes` configuráveis.

Exemplo inicial recomendado:

- `card`

Leitura prática:

- recorrência automática do produto foi desenhada para cartão
- outros métodos dependem de disponibilidade real da conta Stripe, país, moeda e recursos habilitados
- não tratar `pix`, boleto ou similares como base da recorrência antes de validar o modelo operacional

## Webhook

Endpoint esperado:

- `https://SEU_BACKEND/api/v1/billing/stripe/webhook`

Regras operacionais:

- validar assinatura do webhook com `STRIPE_WEBHOOK_SECRET`
- responder com `2xx` apenas quando o evento for aceito
- registrar o evento localmente para idempotência
- usar webhook como fonte final de ativação, renovação, falha e cancelamento

Observação oficial importante:

- a Stripe reenvia webhooks não entregues por até 3 dias

## Customer Portal

O portal deve ser usado para:

- atualização de método de pagamento
- gestão da renovação
- cancelamento no fim do período

Regra local do projeto:

- cancelamento não remove acesso imediatamente
- ele encerra a renovação automática e mantém o acesso até o fim do ciclo já pago

## Fluxo esperado

1. usuário autenticado inicia `POST /billing/checkout`
2. backend cria checkout local
3. backend cria `Checkout Session` na Stripe
4. frontend redireciona para a URL da Stripe
5. Stripe devolve o usuário ao frontend
6. webhook confirma o estado real
7. backend ativa, mantém pendente, suspende ou cancela conforme o evento recebido
8. gestão posterior ocorre via portal

## Teste local com Stripe CLI

Exemplo:

```bash
stripe listen --forward-to http://localhost:5059/api/v1/billing/stripe/webhook
```

Depois, usar o `whsec_...` retornado em:

```env
STRIPE_WEBHOOK_SECRET=whsec_...
```

## O que ainda depende de atuação manual

- criar e configurar a conta Stripe real
- criar os `Products` e `Prices` oficiais na Stripe
- preencher os `Price IDs` correspondentes
- habilitar os métodos de pagamento desejados no painel
- configurar domínio público do frontend e backend
- registrar o webhook no ambiente correto
- preencher secrets/vars no GitHub Actions e/ou VPS
- validar compras reais em sandbox antes de produção

## Checklist de ativação

- [ ] `STRIPE_SECRET_KEY` configurada
- [ ] `STRIPE_WEBHOOK_SECRET` configurada
- [ ] `STRIPE_FRONTEND_BASE_URL` configurada
- [ ] `Price IDs` dos planos pagos configurados
- [ ] webhook criado no painel Stripe
- [ ] ambiente publicado com backend acessível externamente
- [ ] compra de teste validada até o plano ficar ativo
- [ ] portal de cobrança validado
- [ ] cancelamento e falha de pagamento validados em sandbox

## Quando atualizar este documento

- mudança do fluxo de checkout
- mudança dos eventos Stripe tratados
- mudança do modelo de preços configurados
- mudança de portal, cancelamento, retry ou webhook
- mudança de integração que altere a aderência à documentação oficial da Stripe
