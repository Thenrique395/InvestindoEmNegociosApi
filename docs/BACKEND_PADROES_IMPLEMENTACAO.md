# Backend — Padrões de Implementação

Documento normativo para implementação e revisão técnica do backend.

## Objetivo

Este arquivo define o padrão mínimo esperado para:

- implementação de novas features
- correções de bugs
- refactors no backend
- revisão técnica e code review

## Quando consultar este documento

- ao criar ou alterar controller, service, entidade ou integração
- ao revisar se uma mudança respeita a separação entre camadas
- ao decidir onde uma regra de negócio deve morar
- ao validar se a mudança tem testes e cuidados mínimos de persistência e segurança

## Pilares obrigatórios

- SOLID
- Clean Code
- Arquitetura Limpa (camadas e regra de dependência)

Leitura prática:

- código novo deve ser simples, coeso e consistente com as camadas já adotadas
- regra de negócio relevante não deve ser empurrada para controller nem para detalhe de infraestrutura

## Regras obrigatórias de camada

### Regra de dependência

- `Domain` não depende de `Application`, `Infrastructure` ou `Controllers`.
- `Application` não depende de `Infrastructure`.
- `Controllers` não acessam `DbContext` diretamente.

### Responsabilidade por camada

- `Controllers`
  - recebem HTTP
  - validam entrada
  - delegam para serviço ou caso de uso
  - retornam resposta HTTP coerente
- `Application`
  - orquestra casos de uso
  - concentra regra de aplicação e fluxo
- `Domain`
  - guarda conceitos de negócio mais estáveis
  - não conhece detalhes de framework, banco ou transporte
- `Infrastructure`
  - implementa persistência
  - integra serviços externos
  - contém detalhes concretos de execução

## Diretrizes práticas de implementação

- controller fino: valida entrada, delega e retorna HTTP
- regra de negócio no `Application` ou `Domain`, não no controller
- integração externa e persistência no `Infrastructure`
- métodos coesos, nomes explícitos e sem duplicação evitável
- respostas de erro consistentes com `traceId`

## Persistência, performance e segurança

### Persistência

- mudança de modelo exige atualização do SQL versionado em `InvestindoEmNegocio/Infrastructure/Data/schema.sql`
- no fluxo Docker, a criação inicial da base deve acontecer antes da API principal, via execução direta do `schema.sql`
- mudança persistente deve considerar impacto em schema, dados existentes e testes
- tabelas de parâmetros e dados-base devem ter inserts idempotentes
- ao recriar a base, o SQL deve conseguir criar a estrutura e popular o mínimo necessário sem intervenção manual
- se o dado já existir, o script deve inserir apenas o que ainda não existe ou atualizar o que precisar ser complementado com segurança

### Performance

- evitar N+1 e consultas sem filtro em caminhos críticos
- revisar caminhos com alto volume, importação e operações administrativas

### Segurança

- nunca logar segredos, token ou senha
- não relaxar validação, autorização ou proteção de dados para “destravar” entrega

## Testes mínimos por tipo de mudança

- unitário para regra alterada
- integração para fluxo com banco ou endpoint crítico
- cenário feliz e erro esperado

Regra prática:

- mudança pequena ainda precisa proteger a regra que foi tocada
- mudança em fluxo crítico não deve depender só de teste manual

## PR checklist

- [ ] Respeita regras de camada.
- [ ] Sem acesso direto a DbContext no controller.
- [ ] Sem dependência indevida para Infrastructure.
- [ ] Testes adicionados/atualizados.
- [ ] `schema.sql` atualizado quando houver mudança persistente.
- [ ] Inserts idempotentes revisados para tabelas de parâmetros e dados-base.

## Sinais de implementação ruim

- controller com regra de negócio relevante
- serviço de aplicação acoplado a detalhe concreto sem necessidade
- acesso a banco espalhado em pontos errados
- falta de teste em fluxo crítico alterado
- mudança persistente sem atualização do SQL versionado ou sem considerar impacto em dados
