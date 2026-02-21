# API — Contrato de Erros (Backend)

Este documento fixa o padrão de status HTTP para erros no backend.

## Regra geral

- `400 Bad Request`: payload inválido (campo ausente, formato inválido, JSON inválido).
- `401 Unauthorized`: sem autenticação ou credencial/token inválido.
- `403 Forbidden`: autenticado sem permissão.
- `404 Not Found`: recurso/funcionalidade inexistente ou desabilitada.
- `409 Conflict`: conflito de estado (ex.: e-mail já cadastrado).
- `422 Unprocessable Entity`: regra de negócio inválida com payload sintaticamente correto.
- `429 Too Many Requests`: limite de taxa excedido.
- `500 Internal Server Error`: falha inesperada.
- `503 Service Unavailable`: dependência externa indisponível.

## Convenções aplicadas por domínio

- **Auth**
  - login inválido: `401`
  - conta bloqueada: `423`
  - registro com conflito: `409`
  - erro de validação no payload: `400`
- **Data Portability**
  - feature desabilitada: `404`
  - arquivo inválido/JSON inválido/tamanho excedido: `400`
- **Market Data / integrações externas**
  - indisponibilidade do provedor: `503`

## Formato de resposta de erro

Todas as exceções de aplicação devem retornar `application/problem+json`, com:

- `title`
- `status`
- `detail` (quando aplicável)
- `instance`
- `traceId` (sempre)

## Implementação

- Fonte única do mapeamento: `InvestindoEmNegocio/Extensions/ExceptionHandlingExtensions.cs`.
- Exceções de domínio/aplicação: `AppProblemException`.
