# Backend — Padrões de Implementação

Este repositório segue os padrões abaixo em todas as features e correções.

## Pilares obrigatórios

- SOLID
- Clean Code
- Arquitetura Limpa (camadas e regra de dependência)

## Regra de dependência

- `Domain` não depende de `Application`, `Infrastructure` ou `Controllers`.
- `Application` não depende de `Infrastructure`.
- `Controllers` não acessam `DbContext` diretamente.

## Diretrizes práticas

- Controller fino: valida entrada, delega para serviço/caso de uso, retorna HTTP.
- Regra de negócio no `Application`/`Domain`, não no controller.
- Integração externa e persistência no `Infrastructure`.
- Métodos coesos, nomes explícitos e sem duplicação evitável.
- Erros com resposta consistente e `traceId`.

## Persistência e segurança

- Mudança de modelo exige migration.
- Evitar N+1 e consultas sem filtro em caminhos críticos.
- Nunca logar segredos, token ou senha.

## Testes mínimos por mudança

- Unitário para regra alterada.
- Integração para fluxo com banco/endpoint crítico.
- Cenário feliz + erro esperado.

## PR checklist

- [ ] Respeita regras de camada.
- [ ] Sem acesso direto a DbContext no controller.
- [ ] Sem dependência indevida para Infrastructure.
- [ ] Testes adicionados/atualizados.
- [ ] Migration adicionada (quando aplicável).