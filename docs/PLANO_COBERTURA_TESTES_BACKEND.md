# Plano de Cobertura de Testes do Backend

Documento de estratégia e estado atual da suíte backend.

## Estado atual

- Última validação consolidada: `2026-03-10`
- Suíte principal: `InvestindoEmNegocio.Tests`
- Resultado da última rodada completa: `343` testes aprovados
- Comando de referência:

```bash
dotnet test InvestindoEmNegociosApi/InvestindoEmNegocio.sln /p:UseAppHost=false
```

Observação:
- o uso de `/p:UseAppHost=false` continua necessário no ambiente atual por causa do contorno local de `apphost` no macOS

## Cobertura por categoria

### Alta prioridade já coberta

- autenticação e refresh token
- perfil, onboarding e preferências
- planos, parcelas, pagamentos e estornos
- contas, transações, cartões e dívida total
- importações OFX, CSV e fatura
- portabilidade de dados
- investimentos, benchmarks e integrações B3 simuladas
- autorização, policies e smoke de controllers

### Pontos ainda valiosos para expandir

- cenários adicionais de concorrência lógica e consistência transacional
- mais massa de regressão para parser de fatura e extratos
- cenários de erro operacional em integrações externas
- mais testes de performance e carga fora da suíte principal

## Estratégia recomendada

Para mudanças novas de backend:

1. Teste unitário para regra de negócio nova ou alterada.
2. Teste de integração para fluxo que toca banco ou endpoint crítico.
3. Cobertura de caminho feliz, validação de entrada, erro esperado e borda relevante.

## Critério de pronto

Uma mudança backend só fecha quando:

- a regra nova está coberta por teste automatizado
- não há regressão na suíte principal
- contratos HTTP críticos continuam verdes
- a documentação operacional relevante foi atualizada quando aplicável
