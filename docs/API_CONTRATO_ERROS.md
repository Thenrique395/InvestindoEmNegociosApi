# API — Contrato de Erros

Fonte de verdade para o formato e o mapeamento de erros retornados pelo backend.

## Objetivo

Este documento existe para:

- registrar o contrato real de erro da API
- reduzir divergência entre controllers, services e clientes
- deixar claro quando usar cada status HTTP
- evitar respostas inconsistentes entre módulos

## Regra geral

Erros de aplicação devem retornar `application/problem+json`.

O formato esperado hoje é:

- `title`
- `status`
- `detail` quando aplicável
- `instance`
- `traceId` em `extensions.traceId`

Em ambiente de desenvolvimento, a resposta também pode incluir:

- `exception` em `extensions.exception`

## Exemplo de resposta

```json
{
  "title": "Plano inválido",
  "status": 400,
  "detail": "O plano solicitado não está disponível em self-service.",
  "instance": "/api/v1/subscriptions/change",
  "traceId": "0HMPV...",
  "extensions": {
    "traceId": "0HMPV..."
  }
}
```

Observação:

- o campo mais confiável para correlação operacional é `extensions.traceId`
- clientes não devem depender de mensagem textual exata para lógica de negócio

## Mapeamento principal de status

- `400 Bad Request`: payload inválido, parâmetro inválido, arquivo inválido ou regra básica de entrada violada
- `401 Unauthorized`: sem autenticação ou credencial/token inválido
- `402 Payment Required`: operação bloqueada por exigir pagamento ou checkout antes da ativação
- `403 Forbidden`: autenticado sem permissão ou bloqueado por regra de acesso/plano
- `404 Not Found`: recurso não encontrado
- `409 Conflict`: conflito de estado ou duplicidade lógica
- `422 Unprocessable Entity`: payload sintaticamente válido, mas rejeitado por regra de negócio especializada
- `423 Locked`: conta bloqueada
- `429 Too Many Requests`: limite de taxa excedido
- `500 Internal Server Error`: falha inesperada
- `503 Service Unavailable`: dependência externa ou subsistema indisponível

## Distinções importantes

### `400` vs `422`

- `400` deve ser usado quando a entrada já chega inválida ou insuficiente
- `422` deve ser usado quando a entrada é válida do ponto de vista sintático, mas a operação é rejeitada por regra especializada do domínio

Exemplos:

- campo obrigatório ausente, enum inválido, período inválido, arquivo com formato errado: `400`
- importação lida com sucesso, mas rejeitada por consistência de negócio: `422`

### `401` vs `403`

- `401` significa que o usuário não está autenticado corretamente
- `403` significa que o usuário está autenticado, mas não pode executar a ação

Exemplos:

- JWT ausente, expirado ou refresh token inválido: `401`
- usuário autenticado sem permissão, sem feature ou bloqueado por regra do plano: `403`

### `404`

- usar `404` quando o recurso realmente não for encontrado para o usuário e o contexto atual
- não usar `404` como substituto genérico de erro de permissão ou erro de validação

### `402`

- usar `402 Payment Required` quando a operação depender explicitamente de pagamento confirmado ou contratação antes da ativação
- não usar `402` para qualquer erro de billing; erros de integração, configuração ou validação continuam em `400`, `404` ou `503`, conforme o caso

## Convenções por domínio

### Auth

- login com payload inválido: `400`
- credenciais inválidas: `401`
- token inválido ou refresh inválido: `401`
- conta bloqueada: `423`
- registro duplicado: `409`

### Billing e subscriptions

- plano/ciclo inválido: `400`
- tentativa de contratar plano gratuito via checkout: `400`
- assinatura inexistente: `404`
- operação que exige pagamento confirmado antes de ativar plano: `402`
- Stripe não configurado ou billing indisponível: `503`
- webhook inválido: `400`
- portal sem cliente externo vinculado: `400`

### Accounts, cards, goals, plans, loans, budget, scenarios, reports

- dados inválidos de entrada: `400`
- recurso não encontrado: `404`
- conflito lógico de cadastro/estado: `409`, quando aplicável

### Importações e arquivos

- arquivo ausente, vazio ou formato inválido: `400`
- arquivo lido, mas rejeitado por regra do importador: `422`
- falha inesperada no processamento: `500`

### Data portability

- arquivo inválido ou JSON inválido: `400`
- recurso/feature indisponível: `404`

### Market data e integrações externas

- parâmetro inválido: `400`
- provedor indisponível: `503`

## O que evitar

- não usar `500` para erro de validação ou regra de negócio conhecida
- não usar `401` para negar ação de usuário já autenticado
- não usar `404` para mascarar indiscriminadamente erro de autorização
- não criar respostas de erro fora de `application/problem+json` por conveniência local
- não depender de mensagem textual em `detail` como contrato estável entre backend e frontend

## Regras de implementação

- exceções de aplicação devem usar `AppProblemException`
- controllers e services não devem retornar formatos de erro ad hoc
- `UnauthorizedAccessException` não deve ser usada como substituto genérico de regra de negócio
- quando um controller capturar exceção para traduzir status, o mapeamento precisa continuar coerente com este documento

Leitura prática:

- lançar `AppProblemException` quando a aplicação já souber o `status`, `title` e `detail` corretos
- capturar exceções nativas como `ArgumentException`, `InvalidOperationException` ou `UnauthorizedAccessException` apenas para traduzi-las para o contrato correto
- deixar exceções realmente inesperadas seguirem para o middleware global como `500`

## Contrato estável para frontend

O frontend pode assumir com segurança que:

- `status` é a fonte principal para decisão de fluxo
- `title` pode ser usado como mensagem curta ou fallback de UX
- `extensions.traceId` deve existir para suporte e observabilidade

O frontend não deve assumir que:

- `detail` terá texto fixo e imutável
- toda mensagem de erro com mesmo `status` terá exatamente a mesma semântica
- `500` trará contexto suficiente para tratamento fino no cliente

## Implementação atual

- formatação global: [ExceptionHandlingExtensions.cs](../InvestindoEmNegocio/Extensions/ExceptionHandlingExtensions.cs)
- enriquecimento de `ProblemDetails`: [ServiceCollectionExtensions.cs](../InvestindoEmNegocio/Extensions/ServiceCollectionExtensions.cs)
- exceção padrão de aplicação: [AppProblemException.cs](../InvestindoEmNegocio/Application/Exceptions/AppProblemException.cs)

## Cuidados atuais

- alguns fluxos usam `400` para situações que poderiam ser tratadas como `422`; manter consistência é mais importante do que sofisticar cedo demais
- `402 Payment Required` já existe no backend e precisa continuar documentado para não parecer erro inesperado no frontend
- `traceId` deve ser mantido estável em toda resposta de erro para suporte e observabilidade

## Quando atualizar este documento

- mudança no middleware global de erro
- criação de novo status relevante em domínio existente
- criação de novo domínio com comportamento de erro específico
- mudança na estrutura do `ProblemDetails`
