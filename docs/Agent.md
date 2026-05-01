# BackEnd Agent

Agente especializado no backend da API.

## Objetivo

Este agente existe para orientar o Codex a:

- implementar mudancas coerentes com a arquitetura atual da API
- preservar contrato HTTP, autorizacao, persistencia e seguranca
- reduzir drift entre codigo, testes e documentacao normativa
- bloquear solucoes rapidas que quebrem regra de negocio ou operacao

## Escopo

Este agente cuida de:

- `InvestindoEmNegociosApi/InvestindoEmNegocio`
- `InvestindoEmNegociosApi/InvestindoEmNegocio.Tests`
- `InvestindoEmNegociosApi/docs`
- contratos HTTP, autorizacao, persistencia, integracoes e regra de negocio no servidor

## Quando acionar

Use este agente para:

- endpoints, controllers e DTOs
- servicos de aplicacao, dominio e infraestrutura
- autenticacao, refresh token e autorizacao
- billing, Stripe, webhook e assinatura no backend
- migrations, schema, banco e queries
- testes backend, smoke, integracao e cobertura
- pipeline, compose e configuracao operacional da API

## Autoridade deste agente

Dentro do escopo de backend, este arquivo e normativo para o Codex.

Ele define:

- como trabalhar no backend
- quais leituras sao obrigatorias antes de editar
- quais validacoes sao obrigatorias
- quais limites arquiteturais nao podem ser quebrados

Se houver duvida de execucao no backend, este agente prevalece sobre guias genericos.
Se houver conflito com regra operacional geral, `../../Agent.md` prevalece.

## Fontes de verdade

Consultar sempre:

- `../../Agent.md`
- `./BACKEND_PADROES_IMPLEMENTACAO.md`
- `./README.md`
- `../../docs/README.md`
- `../../docs/ARCHITECTURE.md`
- `../../docs/BUSINESS_RULES.md`
- `../../docs/ROADMAP.md`

Consultar quando aplicavel:

- `./API_CONTRATO_ERROS.md`
- `./AUTHORIZATION_MATRIX.md`
- `./DEPLOY_GITHUB_ENVIRONMENTS.md`

## Artefatos do backend

Usar estes pontos de referencia para navegar mais rapido:

- policies e regras de autorizacao: `InvestindoEmNegociosApi/InvestindoEmNegocio/Infrastructure/Auth/`
- controllers HTTP: `InvestindoEmNegociosApi/InvestindoEmNegocio/Controllers/`
- servicos de aplicacao: `InvestindoEmNegociosApi/InvestindoEmNegocio/Application/Services/`
- contratos e interfaces de aplicacao: `InvestindoEmNegociosApi/InvestindoEmNegocio/Application/Interfaces/`
- persistencia e bootstrap de banco: `InvestindoEmNegociosApi/InvestindoEmNegocio/Infrastructure/Data/`
- schema inicial do banco: `InvestindoEmNegociosApi/InvestindoEmNegocio/Infrastructure/Data/schema.sql`
- testes backend: `InvestindoEmNegociosApi/InvestindoEmNegocio.Tests/`
- documentacao normativa local da API: `InvestindoEmNegociosApi/docs/`

## Fluxo obrigatorio de leitura

### Mudanca de endpoint, DTO, status HTTP ou erro

Ler nesta ordem:

1. `../../Agent.md`
2. `./Agent.md`
3. `./BACKEND_PADROES_IMPLEMENTACAO.md`
4. `./API_CONTRATO_ERROS.md`
5. `./README.md`

### Mudanca de policy, role, feature gate, claims ou acesso

Ler nesta ordem:

1. `../../Agent.md`
2. `./Agent.md`
3. `./BACKEND_PADROES_IMPLEMENTACAO.md`
4. `./AUTHORIZATION_MATRIX.md`
5. `../../docs/BUSINESS_RULES.md`

### Mudanca de banco, schema, query, persistencia ou bootstrap

Ler nesta ordem:

1. `../../Agent.md`
2. `./Agent.md`
3. `./BACKEND_PADROES_IMPLEMENTACAO.md`
4. `../../docs/ARCHITECTURE.md`
5. `./README.md`

### Mudanca de billing, assinatura, Stripe, portal ou webhook

Ler nesta ordem:

1. `../../Agent.md`
2. `./Agent.md`
3. `./BACKEND_PADROES_IMPLEMENTACAO.md`
4. `./API_CONTRATO_ERROS.md`
5. `./AUTHORIZATION_MATRIX.md`
6. `../../docs/BUSINESS_RULES.md`
7. `../../docs/ROADMAP.md`

### Mudanca de pipeline, deploy, compose ou ambiente

Ler nesta ordem:

1. `../../Agent.md`
2. `./Agent.md`
3. `./README.md`
4. `./DEPLOY_GITHUB_ENVIRONMENTS.md`
5. `../../docs/RUNBOOK.md`

## Regras obrigatorias

- backend e a fonte final de autorizacao
- controller nao deve concentrar regra de negocio
- mudancas devem respeitar a separacao entre `Domain`, `Application` e `Infrastructure`
- contrato HTTP, policy e comportamento de erro precisam ficar coerentes com a documentacao normativa da API
- toda mudanca sensivel deve preservar consistencia entre cobranca, assinatura e acesso
- alteracao persistente nao pode ficar so em configuracao local ou em memoria; o bootstrap real do ambiente deve continuar coerente
- o Codex deve preferir a menor mudanca coerente com a arquitetura atual, sem criar atalhos fora do padrao

## Como decidir o escopo

Resolver so no backend quando:

- a mudanca e puramente de regra de negocio, persistencia, autorizacao ou integracao de servidor
- o contrato externo nao muda
- o frontend ja consome o comportamento esperado

Considerar impacto compartilhado com frontend quando:

- endpoint, DTO, status HTTP ou erro mudar
- policy ou feature gate mudar e afetar UX, navegacao ou menus
- fluxo de billing, onboarding ou autenticacao mudar

Parar e escalar quando:

- a regra de negocio estiver ambigua nos documentos normativos
- houver conflito entre codigo atual e direcao de produto sem decisao registrada
- uma mudanca de backend exigir decisao comercial, juridica ou operacional que nao esteja documentada

## Criterios de bloqueio

O Codex nao deve seguir adiante sem registrar o problema quando houver:

- mudanca persistente sem atualizar o fluxo real de schema ou bootstrap
- mudanca de acesso sem revisar `./AUTHORIZATION_MATRIX.md`
- mudanca de endpoint, status HTTP ou erro sem revisar `./API_CONTRATO_ERROS.md`
- mudanca sensivel de billing sem revisar impacto em assinatura, retry, reconciliacao, downgrade e acesso
- mudanca que empurre regra critica para o frontend
- mudanca que libere acesso premium apenas por UX

## Checklist por tipo de mudanca

### Endpoint, controller, DTO ou contrato HTTP

- revisar controller e servico impactado
- revisar `./API_CONTRATO_ERROS.md`
- revisar consumidores impactados
- validar status HTTP, payload e comportamento de erro

### Policy, role, feature gate ou claims

- revisar `./AUTHORIZATION_MATRIX.md`
- revisar `AppAuthorizationPolicies`
- revisar impacto de `Admin`, overrides e claims transformadas
- validar testes de autorizacao e smoke do endpoint

### Banco, schema, query ou persistencia

- revisar entidade, repositorio, query e fluxo de bootstrap
- revisar `schema.sql` quando houver impacto persistente
- validar compatibilidade com compose, ambiente local e inicializacao limpa

### Billing, Stripe, assinatura ou acesso pago

- revisar estados de assinatura e reconciliacao
- revisar retry, cancelamento, downgrade e acesso resultante
- revisar `./API_CONTRATO_ERROS.md` e `./AUTHORIZATION_MATRIX.md` se houver impacto de acesso
- validar fluxo sensivel com teste ou verificacao objetiva

### Pipeline, deploy, compose ou ambiente

- revisar `docker-compose.yml`, workflow e documentacao operacional impactada
- preservar promocao segura entre `development` e `production`
- nunca embutir segredo em codigo ou documento versionado

## Validacao minima

Executar conforme aplicavel:

- `dotnet build InvestindoEmNegociosApi/InvestindoEmNegocio/InvestindoEmNegocio.csproj`
- `dotnet test InvestindoEmNegociosApi/InvestindoEmNegocio.sln /p:UseAppHost=false`
- validacao adicional de compose, webhook, deploy ou integracoes externas quando a tarefa exigir

## Formato esperado da entrega

Ao fechar uma tarefa de backend, o Codex deve informar:

- o que foi alterado
- quais validacoes executou
- quais documentos normativos foram revisados ou atualizados
- quais riscos residuais ou limites permanecem

## Nao fazer

- mover regra critica para o frontend
- liberar feature premium so por UX
- alterar billing sem revisar estados, retry, downgrade e reconciliacao
- mudar contrato de API sem revisar consumidores
- editar documentacao normativa como maquiagem sem alinhar a implementacao real
