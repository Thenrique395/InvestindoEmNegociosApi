# Plano de cobertura de testes (backend)

## Escopo atual

- Suite unitária: `InvestindoEmNegocio.Tests`
- Resultado atual: `115/115` testes passando
- Observação: este plano usa cobertura **funcional/comportamental** por serviço (não é relatório de cobertura por linha).

## Prioridade 1 (crítico)

Serviços com impacto direto em segurança, autenticação, movimentação financeira e dados do usuário.

- `AuthService` (coberto)
  - Já cobre:
    - e-mail duplicado, login inválido, logout/revogação;
    - rotação de refresh token (`RefreshAsync`) com token válido e inválido;
    - bloqueio de conta após 5 tentativas (lockout);
    - troca de senha com senha atual incorreta/correta.

- `InvestmentsService` (coberto)
  - Já cobre:
    - validação de alocação, regras de venda acima da posição, defaults;
    - atualização de preço médio em `COMPRA`;
    - redução de quantidade em `VENDA/RESGATE`;
    - `EnrichWithMarketAsync` (falha do provider e cenário de sucesso com snapshot).

- `DataPortabilityService` (coberto)
  - Já cobre:
    - export com cache e import básico vazio;
    - import real com snapshot completo (plans/installments/payments/goals/positions);
    - `replaceExisting = true` removendo dados antigos;
    - validação de arquivo inválido (JSON quebrado / campos obrigatórios ausentes).

- `B3ImportService` / `B3SyncService` (coberto)
  - Já cobre:
    - consentimento, fallback, import de posição nova, token inválido;
    - estratégia `replace` removendo posições já importadas;
    - deduplicação de movimentos (evitar duplicado no mesmo ativo/data/valor);
    - `ConfirmAsync` com token de outro usuário (deve negar).

## Prioridade 2 (alta)

Serviços de operações centrais do domínio (planos, parcelas, metas, notificações).

- `PlansService`, `InstallmentsService`, `GoalsService`, `GoalContributionsService`, `NotificationsService` (cobertos)
- Focos restantes:
  - fluxos de borda em datas (virada de mês, vencido vs futuro);
  - cenários de concorrência lógica (duplo pagamento, dupla marcação como lida);
  - validações de transição de estado (`Planned -> InProgress -> Completed/Canceled`).

## Prioridade 3 (média)

Serviços de administração e parâmetros.

- `AdminUsersService`, `AdminCategoriesService`, `AdminParametersService`, `LookupsService`, `PreferencesService`, `OnboardingService`, `ProfileService`, `CardsService`, `CategoriesService`, `IncomeSummaryService` (cobertos)
- Focos restantes:
  - avaliar necessidade de tratar e padronizar falhas de persistência (`DbUpdateException`) em serviços admin que hoje não fazem tradução para `AppProblemException` (`AdminUsersService` e `AdminCategoriesService`);
  - validação forte de entrada em update parcial;
  - garantir mensagens/erros consistentes para contratos de API.

## Prioridade 4 (baixa)

Serviços auxiliares, observabilidade e parser.

- `AuditService`, `MarketDataService`, `InvestmentBenchmarksService`, `InvoiceImportService`, `AuthFacadeService`, `DataPortabilityFacadeService`, `InvestmentsFacadeService` (cobertos)
- Focos restantes:
  - parser de fatura com PDF de amostra real (snapshot de regressão);
  - benchmark com diferentes formatos de retorno do BCB;
  - cache hit/miss com clock controlado para evitar flakiness.

## Ordem sugerida de execução (status)

1. ✅ Completar `AuthService` (`RefreshAsync`, lockout e `ChangePasswordAsync`).
2. ✅ Completar `InvestmentsService` (média/quantidade e enrich de mercado).
3. ✅ Completar `DataPortabilityService` (import completo + `replaceExisting`).
4. ✅ Completar `B3ImportService` (`replace`, deduplicação, token de outro usuário).
5. 🟡 Endurecer serviços de admin com testes de exceção de persistência (parcialmente concluído em `AdminParametersService`).

## Critério de pronto para backend

- Todos os serviços com testes de:
  - caminho feliz;
  - validação de entrada;
  - erro de domínio;
  - falha de infraestrutura esperada.
- Contratos HTTP validados para erros (`ProblemDetails`) nos endpoints críticos.
- Pipeline CI executando testes unitários em toda PR.
