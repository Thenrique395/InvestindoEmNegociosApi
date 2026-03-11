# Documentação do Backend

Este diretório concentra os documentos normativos e operacionais da API.

## Fonte de verdade

- Padrões de implementação: [BACKEND_PADROES_IMPLEMENTACAO.md](./BACKEND_PADROES_IMPLEMENTACAO.md)
- Matriz de autorização: [AUTHORIZATION_MATRIX.md](./AUTHORIZATION_MATRIX.md)
- Contrato de erros da API: [API_CONTRATO_ERROS.md](./API_CONTRATO_ERROS.md)
- Plano de cobertura de testes: [PLANO_COBERTURA_TESTES_BACKEND.md](./PLANO_COBERTURA_TESTES_BACKEND.md)
- Smoke e playbooks operacionais:
  - [SMOKE_TESTS_SUITE.md](./SMOKE_TESTS_SUITE.md)
  - [FLUXO_SALDO_TRANSACOES_PLAYBOOK.md](./FLUXO_SALDO_TRANSACOES_PLAYBOOK.md)

## Regra de manutenção

- Padrões técnicos do backend vivem apenas aqui.
- Mudança de policy ou acesso exige atualização simultânea de código e `AUTHORIZATION_MATRIX.md`.
- Mudança relevante em comportamento de erro exige atualização de `API_CONTRATO_ERROS.md`.
