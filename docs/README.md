# Documentação do Backend

Este diretório concentra os documentos normativos e operacionais da API.

## Objetivo deste índice

Este arquivo existe para:

- apontar a fonte de verdade técnica do backend
- separar documentação central de produto da documentação específica da API
- reduzir duplicação entre código, playbooks e contratos
- deixar claro quando consultar cada documento deste diretório

## Leitura rápida

Use este diretório quando a dúvida for sobre:

- contrato HTTP, payload e comportamento de erro
- autorização real por role, feature ou policy
- padrão de implementação do backend
- playbooks operacionais e documentação técnica específica da API
- operação técnica da API, incluindo billing Stripe

Não use este diretório como fonte principal para:

- oferta comercial
- ICP, planos e copy
- regra transversal de produto fora do escopo técnico da API
- documentação do frontend

## Visão rápida da API

Esta aplicação é a API principal do produto `Investindo em Negócios`.

Ela cobre:

- autenticação, sessão e autorização
- onboarding, perfil, preferências e LGPD
- despesas, receitas, metas, contas, cartões e ledger
- importações financeiras e portabilidade
- investimentos, patrimônio, empréstimos e snapshots
- billing, assinatura e integração com Stripe
- área administrativa, parâmetros e robôs

## Visão resumida da solução

Componentes principais da solução:

- backend API: `InvestindoEmNegociosApi/InvestindoEmNegocio`
- frontend web consumidor: `../../InvestindoEmNegociosWeb/investindoEmNegociosWeb`
- banco principal: PostgreSQL

Fluxo principal:

1. o frontend autentica e consome a API
2. a API aplica autorização, regra de negócio e integrações externas
3. a persistência principal acontece no PostgreSQL

## Stack principal

- ASP.NET Core 9
- EF Core + Npgsql
- JWT + refresh token
- OpenAPI/Scalar
- Serilog + OpenTelemetry
- Stripe para billing
- Docker Compose para ambiente local

## Relação com a documentação central

- o mapa principal da documentação compartilhada desta pasta de trabalho está em [../../docs/README.md](../../docs/README.md)
- estratégia de produto, oferta, planos e regras comerciais vivem no `docs/` da raiz
- este diretório deve conter apenas documentação técnica e operacional específica da API
- quando houver conflito, contratos e padrões do backend continuam sendo fonte final para comportamento técnico da API

Regra prática:

- use o `docs/README.md` da raiz para descobrir qual documento consultar
- use este diretório quando a dúvida for específica da API
- se a dúvida for de produto ou oferta, volte para a documentação central

## Quando consultar este diretório

- ao mudar contrato HTTP, status code ou payload de erro
- ao mexer em policies, roles, features ou autorização efetiva
- ao revisar padrões de implementação do backend
- ao validar cobertura, smoke tests ou playbooks operacionais
- ao operar billing Stripe, saldo/transações ou fluxos críticos específicos da API

## Documentos principais

### Normativos

- [BACKEND_PADROES_IMPLEMENTACAO.md](./BACKEND_PADROES_IMPLEMENTACAO.md)
  - usar para padrões de código, limites de camada e expectativas mínimas por mudança
- [AUTHORIZATION_MATRIX.md](./AUTHORIZATION_MATRIX.md)
  - usar para acesso real por endpoint, role, feature e policy
- [API_CONTRATO_ERROS.md](./API_CONTRATO_ERROS.md)
  - usar para formato, consistência e comportamento de erros da API

### Playbooks operacionais

- [BILLING_STRIPE_PLAYBOOK.md](./BILLING_STRIPE_PLAYBOOK.md)
  - usar para operação técnica do Stripe, webhook, portal e configuração externa

### Schema e persistência versionada

- o SQL versionado do backend fica em `InvestindoEmNegocio/Infrastructure/Data/schema.sql`
- esse arquivo é a referência SQL única para evolução persistente do banco neste repositório
- no fluxo Docker, o banco deve ficar saudável primeiro, depois um bootstrap SQL via `psql` aplica o `schema.sql` e só então a API principal sobe
- nesse fluxo, a API principal sobe com bootstrap de schema desabilitado

## Como validar

- build: `dotnet build InvestindoEmNegociosApi/InvestindoEmNegocio/InvestindoEmNegocio.csproj`
- testes: `dotnet test InvestindoEmNegociosApi/InvestindoEmNegocio.sln /p:UseAppHost=false`
- compose: `docker compose -f InvestindoEmNegociosApi/docker-compose.yml config`

## Operação e ambiente

Informações de endpoint público e credenciais podem variar por ambiente e não devem ser tratadas como fonte fixa neste arquivo.

Use:

- `appsettings` e variáveis de ambiente para configuração
- `docker-compose.yml` para bootstrap local
- health checks e OpenAPI do ambiente ativo para validação operacional
- documentação central em `../../docs/` para direção de produto e regras transversais

## Atalho por tipo de dúvida

- como implementar no backend: `BACKEND_PADROES_IMPLEMENTACAO.md`
- quem pode acessar o quê: `AUTHORIZATION_MATRIX.md`
- como a API deve responder erro: `API_CONTRATO_ERROS.md`
- como operar billing Stripe: `BILLING_STRIPE_PLAYBOOK.md`

## O que não deve viver aqui

- copy comercial
- definição de oferta, planos e ICP
- regra comercial transversal já consolidada no `docs/` central
- documentação do frontend web

Regra prática:

- se a dúvida for sobre comportamento do produto como oferta, comece no `../../docs/PRODUCT.md`
- se a dúvida for sobre regra funcional transversal, comece no `../../docs/BUSINESS_RULES.md`
- se a dúvida for sobre estrutura técnica da API, contratos ou operação backend, use este diretório

## Regra de manutenção

- padrões técnicos do backend vivem apenas aqui
- mudança de policy ou acesso exige atualização simultânea de código e `AUTHORIZATION_MATRIX.md`
- mudança relevante em comportamento de erro exige atualização de `API_CONTRATO_ERROS.md`
- mudança em playbook operacional exige atualização do documento correspondente neste diretório
- se um novo documento técnico do backend passar a ser consultado com frequência, ele deve ser listado neste índice

## Quando atualizar este README

- quando surgir um novo documento normativo do backend
- quando a função de um documento existente mudar
- quando o fluxo principal de consulta do diretório deixar de refletir a realidade
