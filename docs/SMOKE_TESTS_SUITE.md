# Smoke Tests - Suite Global

Suite de validação rápida para cobertura dos fluxos críticos da aplicação (API + Web).

## Escopo coberto
- Autenticação e contrato de erro de API.
- Perfil/onboarding/preferências (incluindo LGPD self-service).
- Receitas/despesas/parcelas/pagamentos/estornos.
- Cartões e fatura por competência.
- Contas, saldo por conta e transferência formal entre contas.
- Endpoints administrativos principais.

## Execução rápida (local)

### 1) Backend smoke (foco em fluxos core + controllers)
```bash
dotnet test InvestindoEmNegocio.Tests/InvestindoEmNegocio.Tests.csproj --filter "FullyQualifiedName~MoreControllersSmokeTests|FullyQualifiedName~AccountsServiceTests|FullyQualifiedName~CardsServiceTests|FullyQualifiedName~ProfileControllerIntegrationTests|FullyQualifiedName~ApiErrorContractIntegrationTests"
```

### 2) Frontend smoke (unit smoke + build)
```bash
cd ../InvestindoEmNegociosWeb/investindoEmNegociosWeb
npm run test -- --watch=false --browsers=ChromeHeadless
npm run build
```

## Cenários adicionais já documentados
- `InvestindoEmNegociosWeb/investindoEmNegociosWeb/documentacao/Cenarios de teste/4.3_fatura-por-competencia.md`
- `InvestindoEmNegociosWeb/investindoEmNegociosWeb/documentacao/Cenarios de teste/5.1_tipos-transacao-transferencia.md`

## Critério de aprovação
- Backend: 100% verde no comando de smoke.
- Front: testes unitários smoke verdes + build sem erro.
- Sem regressão visual/funcional crítica nas telas:
  - `Contas`
  - `Cartões`
  - `Despesas`
  - `Receitas`
  - `Home`
