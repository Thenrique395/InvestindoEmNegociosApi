# Backend — Padrões de Implementação

Documento normativo para implementação e revisão técnica do backend real deste projeto.

## Objetivo

Este arquivo define o padrão obrigatório para:

- implementação de novas features
- correções de bugs
- refactors no backend
- revisão técnica e code review
- mudanças de contrato, persistência, autorização, billing e integrações

## O que este documento governa

Este documento deve responder:

- onde cada mudança deve ser implementada
- quando uma mudança exige revisão de contrato, autorização ou schema
- quais validações mínimas são obrigatórias
- quando a mudança deve ser bloqueada até ajuste adicional

Este documento não existe para repetir direção de produto ou arquitetura central.

Use também:

- `./API_CONTRATO_ERROS.md`
- `./AUTHORIZATION_MATRIX.md`

## Quando consultar este documento

- ao criar ou alterar controller, service, entidade, integração ou fluxo persistente
- ao revisar se uma mudança respeita a separação entre camadas
- ao decidir onde uma regra de negócio deve morar
- ao mudar contrato HTTP, autorização, comportamento de erro ou billing
- ao validar se a mudança tem testes e cuidados mínimos de persistência e segurança

## Regras obrigatórias de implementação

### Camadas e dependência

- `Domain` não depende de `Application`, `Infrastructure` ou `Controllers`
- `Application` não depende de `Infrastructure` diretamente por detalhe concreto
- `Controllers` não acessam `DbContext` diretamente
- integrações externas e persistência concreta ficam em `Infrastructure`
- regra de negócio relevante não deve ser empurrada para controller nem para detalhe de infraestrutura

### Responsabilidade por camada

- `Controllers`
  - recebem HTTP
  - traduzem request/response
  - aplicam autenticação, autorização e validação de entrada compatíveis com o contrato
  - delegam para serviço, fluxo ou caso de uso
- `Application`
  - orquestra casos de uso
  - concentra regra de aplicação, fluxo e coordenação entre dependências
  - decide o comportamento funcional da operação
- `Domain`
  - guarda conceitos de negócio mais estáveis
  - não conhece framework, banco, transporte ou detalhe operacional
- `Infrastructure`
  - implementa persistência
  - integra serviços externos
  - executa detalhes concretos de banco, auth, logging e integração

### Regras práticas de código

- controller fino: valida entrada, delega e retorna HTTP coerente
- métodos devem ser coesos, com nomes explícitos e sem duplicação evitável
- não espalhar acesso a banco em pontos arbitrários da aplicação
- não criar retorno de erro ad hoc fora do contrato normativo da API
- respostas de erro devem permanecer consistentes com `traceId`

## Regras obrigatórias de contrato HTTP

Toda mudança que tocar endpoint, DTO, payload, status code, policy ou erro deve revisar:

- `API_CONTRATO_ERROS.md`
- `AUTHORIZATION_MATRIX.md`
- consumidores impactados

Regras:

- não mudar comportamento HTTP crítico sem revisar documentação normativa
- não introduzir resposta nova fora de `application/problem+json` por conveniência local
- não usar exceção genérica como atalho para semântica de erro que a aplicação já conhece
- não mudar rota, payload ou status de endpoint consumido sem avaliar impacto em frontend, testes e documentação

## Regras obrigatórias de autorização

Toda mudança funcional sensível deve avaliar:

- policy usada
- role mínima efetiva
- feature gate
- impacto na matriz de autorização

Regras:

- backend continua sendo a fonte final de autorização
- mudança de `[Authorize]`, policy, feature ou proteção equivalente exige atualização da matriz normativa
- frontend pode refletir UX, mas não substitui a proteção real
- não liberar funcionalidade premium apenas por ocultação ou exibição em cliente

## Regras obrigatórias de persistência e schema

Toda mudança persistente exige revisão explícita de:

- `InvestindoEmNegocio/Infrastructure/Data/schema.sql`
- impacto em dados existentes
- impacto em bootstrap local e Docker
- impacto em testes de integração

Regras:

- mudança de modelo não pode ficar só em configuração local ou código sem refletir no SQL versionado
- o `schema.sql` deve continuar suficiente para criar a base do zero no fluxo suportado do projeto
- inserts de parâmetros e dados-base devem permanecer idempotentes
- mudança destrutiva ou sensível exige estratégia explícita, não edição silenciosa
- se o dado já existir, o script deve inserir apenas o que ainda não existe ou complementar com segurança

## Regras obrigatórias de billing, auth e integrações

Mudanças nesses domínios exigem cuidado adicional.

### Billing

- não alterar fluxo de cobrança sem revisar estados, retry, downgrade, reconciliação e documentação
- não permitir ativação de premium sem base real de pagamento confirmado
- mudança em checkout, portal, webhook ou sincronização exige validação além de teste manual simples

### Auth e segurança

- não relaxar validação, autorização ou proteção de dados para destravar entrega
- não usar `UnauthorizedAccessException` como atalho genérico de regra de negócio
- não expor segredo, token, senha ou dado sensível em log

### Integrações externas

- falha de dependência externa deve manter semântica de erro coerente
- integração crítica exige caminho de erro explícito e observável

### Robôs (`IRobotTask`)

- robôs de automação implementam `IRobotTask` (`Application/Interfaces/IRobotTask.cs`) e retornam `RobotTaskExecutionResult`
- exemplos atuais: `MonthlySnapshotRobotTask`, `SubscriptionExpirationRobotTask`, `ReminderRobotTask`
- quando um robô decide aplicar um efeito penalizador ou irreversível (ex.: downgrade de assinatura por `PastDue`) com base em estado que pode estar desatualizado, ele deve consultar a fonte externa autoritativa antes de agir (`GetSubscriptionAsync` do gateway resolvido via `IPaymentProviderResolver`/`UserSubscription.Provider` — Stripe ou Mercado Pago, conforme a assinatura)
- se a fonte externa estiver indisponível, o robô deve degradar graciosamente e decidir pelo estado local, registrando log de aviso — nunca falhar silenciosamente nem travar a execução agendada
- robôs são administráveis via `/admin/robots` (execução manual, monitor de execução)

### LGPD e exclusão de conta

- exclusão de conta self-service usa anonimização (`User.Anonymize`), não exclusão física do registro de `users` — preserva auditoria e estatísticas sem manter PII
- `UserSubscriptions`, `BillingCheckouts` e `AuditLogs` são retidos pós-exclusão por exigência de retenção fiscal/trilha de segurança; não devem ser apagados em `RemoveUserDataAsync`
- qualquer nova entidade vinculada ao usuário precisa decidir explicitamente entre anonimização, retenção ou remoção — não assumir remoção física por padrão

## Regras mínimas de testes por tipo de mudança

### Regra ou fluxo interno

- teste unitário para a regra alterada
- cenário feliz e erro esperado

### Endpoint, banco ou fluxo crítico

- teste de integração para fluxo com banco ou endpoint crítico
- cobertura mínima do caminho principal e da rejeição esperada

### Billing, auth ou autorização

- não depender só de teste manual
- validar ao menos comportamento protegido, status esperado e erro coerente

Regra prática:

- mudança pequena ainda precisa proteger a regra tocada
- mudança crítica não deve sair só com confiança implícita

## Critérios de bloqueio

A mudança deve ser considerada incompleta quando ocorrer qualquer um destes casos:

- mudança de contrato sem revisão de consumidores ou docs normativas
- mudança de autorização sem revisão da matriz
- mudança persistente sem atualização de `schema.sql`
- mudança de billing sem revisão dos estados e impactos operacionais
- fluxo crítico alterado sem testes proporcionais
- controller recebendo regra de negócio relevante ou acesso direto indevido a banco

## Checklist de PR

- [ ] Respeita regras de camada.
- [ ] Sem acesso direto a `DbContext` no controller.
- [ ] Sem dependência indevida para detalhe concreto de `Infrastructure`.
- [ ] Contrato HTTP revisado quando a mudança tocar endpoint, DTO, status ou erro.
- [ ] Matriz de autorização revisada quando a mudança tocar policy, role ou feature.
- [ ] `schema.sql` atualizado quando houver mudança persistente.
- [ ] Inserts idempotentes revisados para tabelas de parâmetros e dados-base.
- [ ] Testes adicionados ou atualizados de forma proporcional ao risco.
- [ ] Logs e segredos revisados.

## Sinais de implementação ruim

- controller com regra de negócio relevante
- serviço de aplicação acoplado a detalhe concreto sem necessidade
- acesso a banco espalhado em pontos errados
- falta de teste em fluxo crítico alterado
- mudança persistente sem atualização do SQL versionado ou sem considerar impacto em dados
- mudança de contrato ou autorização sem revisão documental correspondente
