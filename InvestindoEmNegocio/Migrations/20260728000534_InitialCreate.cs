using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    InitialBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.CheckConstraint("ck_accounts_initial_balance", "\"InitialBalance\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "billing_checkouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RoleRequested = table.Column<int>(type: "integer", nullable: false),
                    BillingCycle = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderCheckoutId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProviderCustomerId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProviderSubscriptionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProviderPaymentIntentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProviderPaymentStatus = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    LastProviderEventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmailSuccessSent = table.Column<bool>(type: "boolean", nullable: false),
                    EmailPendingSent = table.Column<bool>(type: "boolean", nullable: false),
                    EmailFailureSent = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_checkouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "billing_webhook_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingCheckoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    Processed = table.Column<bool>(type: "boolean", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "card_brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AppliesTo = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "goal_contributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_contributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ExpectedMonthly = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Recurrence = table.Column<string>(type: "text", nullable: false),
                    WarningThreshold = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CriticalThreshold = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "institutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investment_allocation_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rf = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Acoes = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Fundos = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Cripto = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_allocation_targets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investment_goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "loan_contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    AnnualInterestRate = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: false),
                    AmortizationType = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDay = table.Column<int>(type: "integer", nullable: false),
                    MonthlyPayment = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalInterest = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "monthly_budget_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_budget_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "monthly_financial_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    SnapshotLabel = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    RealAvailableBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ProjectedBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PendingExpenses = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PendingIncomes = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalDebt = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    NetWorth = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskClassification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PrimaryInsight = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RecommendationsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_financial_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeUpcomingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IncomeDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    ExpenseUpcomingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExpenseDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    ExpenseOverdueEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CardCloseSoonEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CardCloseDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    CardCloseDayEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MonthCloseEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MonthSummaryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GoalBelowExpectedEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GoalCompletedEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GoalInactivityEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GoalInactivityDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "robot_execution_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RobotName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HostName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    EmailsAttempted = table.Column<int>(type: "integer", nullable: false),
                    EmailsSent = table.Column<int>(type: "integer", nullable: false),
                    EmailsFailed = table.Column<int>(type: "integer", nullable: false),
                    ZeroItemsReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WasSkipped = table.Column<bool>(type: "boolean", nullable: false),
                    SkipReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robot_execution_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "robot_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DailyRunTimeUtc = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_robot_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_categorization_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    NormalizedPattern = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hits = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FirstLearnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLearnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_categorization_feedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstallmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    MoneyType = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ReferenceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_onboarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_onboarding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarryOverDay = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FinancialGoal = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IntelligenceMode = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false, defaultValue: "B"),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NotifyUpcomingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOverdueEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyEmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyInAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyDaysBeforeDue = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RoleGranted = table.Column<int>(type: "integer", nullable: false),
                    BillingCycle = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PriceAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalCustomerId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExternalSubscriptionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExternalPriceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RenewsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Document = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false, defaultValue: ""),
                    AvatarUrl = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockoutUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAnonymized = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "account_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_transactions", x => x.Id);
                    table.CheckConstraint("ck_account_transactions_amount_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_account_transactions_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<int>(type: "integer", nullable: false),
                    Bank = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    StatementCloseDay = table.Column<int>(type: "integer", nullable: false),
                    DueDay = table.Column<int>(type: "integer", nullable: false),
                    HolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nickname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cards_card_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "card_brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goal_occurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_occurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goal_occurrences_goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goal_scopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "text", nullable: false),
                    RefId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goal_scopes_goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investment_positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Asset = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    AvgPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    Account = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    InstitutionId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investment_positions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "loan_installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentNo = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BeginningBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    EndingBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_installments_loan_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "loan_contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Entity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_feature_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_feature_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_feature_overrides_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "money_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Schedule = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: true),
                    InstallmentsCount = table.Column<int>(type: "integer", nullable: true),
                    DefaultPaymentMethodId = table.Column<int>(type: "integer", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_money_plans", x => x.Id);
                    table.CheckConstraint("ck_money_plans_schedule", "(\"Schedule\" = 'OneTime' AND \"InstallmentsCount\" = 1 AND \"Frequency\" IS NULL) OR (\"Schedule\" = 'Installments' AND \"InstallmentsCount\" >= 2 AND \"Frequency\" IS NULL) OR (\"Schedule\" = 'Recurring' AND \"InstallmentsCount\" IS NULL AND \"Frequency\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_money_plans_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_money_plans_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "investment_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investment_movements_investment_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "investment_positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "money_installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentNo = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StatementYear = table.Column<int>(type: "integer", nullable: true),
                    StatementMonth = table.Column<int>(type: "integer", nullable: true),
                    StatementCloseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StatementDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_money_installments", x => x.Id);
                    table.CheckConstraint("ck_installment_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("ck_installment_no_positive", "\"InstallmentNo\" >= 1");
                    table.ForeignKey(
                        name: "FK_money_installments_money_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "money_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "money_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    MethodId = table.Column<int>(type: "integer", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_money_payments", x => x.Id);
                    table.CheckConstraint("ck_payment_amount_positive", "\"PaidAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_money_payments_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_money_payments_money_installments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "money_installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_AccountId_OccurredAt",
                table: "account_transactions",
                columns: new[] { "AccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_SourceType_SourceId",
                table: "account_transactions",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_UserId_OccurredAt",
                table: "account_transactions",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_account_transactions_UserId_SpaceId_OccurredAt",
                table: "account_transactions",
                columns: new[] { "UserId", "SpaceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_UserId_SpaceId_IsActive",
                table: "accounts",
                columns: new[] { "UserId", "SpaceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_UserId_SpaceId_Name",
                table: "accounts",
                columns: new[] { "UserId", "SpaceId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_checkouts_ProviderCheckoutId",
                table: "billing_checkouts",
                column: "ProviderCheckoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_checkouts_ProviderSubscriptionId",
                table: "billing_checkouts",
                column: "ProviderSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_checkouts_UserId",
                table: "billing_checkouts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_billing_checkouts_UserId_Status_CreatedAt",
                table: "billing_checkouts",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_webhook_events_Provider_ProviderEventId",
                table: "billing_webhook_events",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_webhook_events_ReceivedAt",
                table: "billing_webhook_events",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_card_brands_Code",
                table: "card_brands",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_BrandId",
                table: "cards",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_cards_UserId_BrandId_Last4",
                table: "cards",
                columns: new[] { "UserId", "BrandId", "Last4" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_categories_UserId_Name",
                table: "categories",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goal_contributions_GoalId_Date",
                table: "goal_contributions",
                columns: new[] { "GoalId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_goal_occurrences_GoalId",
                table: "goal_occurrences",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_goal_occurrences_GoalId_PeriodStart",
                table: "goal_occurrences",
                columns: new[] { "GoalId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_goal_occurrences_GoalId_Sequence",
                table: "goal_occurrences",
                columns: new[] { "GoalId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goal_scopes_GoalId",
                table: "goal_scopes",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_goal_scopes_GoalId_ScopeType",
                table: "goal_scopes",
                columns: new[] { "GoalId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_goals_UserId_Kind",
                table: "goals",
                columns: new[] { "UserId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_goals_UserId_SpaceId",
                table: "goals",
                columns: new[] { "UserId", "SpaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_goals_UserId_Status",
                table: "goals",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_goals_UserId_Year",
                table: "goals",
                columns: new[] { "UserId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_institutions_Name_Type",
                table: "institutions",
                columns: new[] { "Name", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_allocation_targets_UserId",
                table: "investment_allocation_targets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_goals_UserId",
                table: "investment_goals",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_movements_PositionId",
                table: "investment_movements",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_investment_positions_InstitutionId",
                table: "investment_positions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_investment_positions_UserId_Asset",
                table: "investment_positions",
                columns: new[] { "UserId", "Asset" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_contracts_UserId_CreatedAt",
                table: "loan_contracts",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_contracts_UserId_Status",
                table: "loan_contracts",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_installments_ContractId_InstallmentNo",
                table: "loan_installments",
                columns: new[] { "ContractId", "InstallmentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_installments_UserId_DueDate",
                table: "loan_installments",
                columns: new[] { "UserId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_money_installments_PlanId_DueDate",
                table: "money_installments",
                columns: new[] { "PlanId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_money_installments_PlanId_InstallmentNo",
                table: "money_installments",
                columns: new[] { "PlanId", "InstallmentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_money_installments_UserId_DueDate",
                table: "money_installments",
                columns: new[] { "UserId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_money_installments_UserId_SpaceId_DueDate",
                table: "money_installments",
                columns: new[] { "UserId", "SpaceId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_money_payments_AccountId",
                table: "money_payments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_money_payments_InstallmentId",
                table: "money_payments",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_money_payments_UserId_PaidAt",
                table: "money_payments",
                columns: new[] { "UserId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_money_plans_CardId",
                table: "money_plans",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_money_plans_CategoryId",
                table: "money_plans",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_money_plans_UserId",
                table: "money_plans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_money_plans_UserId_SpaceId",
                table: "money_plans",
                columns: new[] { "UserId", "SpaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_items_UserId_Year_Month",
                table: "monthly_budget_items",
                columns: new[] { "UserId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_items_UserId_Year_Month_CategoryName",
                table: "monthly_budget_items",
                columns: new[] { "UserId", "Year", "Month", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_financial_snapshots_UserId_Year_Month",
                table: "monthly_financial_snapshots",
                columns: new[] { "UserId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_TokenHash",
                table: "password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_UserId",
                table: "password_reset_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_robot_execution_logs_RobotName_StartedAt",
                table: "robot_execution_logs",
                columns: new[] { "RobotName", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_spaces_UserId",
                table: "spaces",
                column: "UserId",
                unique: true,
                filter: "\"IsDefault\" AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_spaces_UserId_IsDefault",
                table: "spaces",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_user_categorization_feedback_UserId_Type_LastLearnedAt",
                table: "user_categorization_feedback",
                columns: new[] { "UserId", "Type", "LastLearnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_categorization_feedback_UserId_Type_NormalizedPattern",
                table: "user_categorization_feedback",
                columns: new[] { "UserId", "Type", "NormalizedPattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_feature_overrides_UserId_FeatureKey",
                table: "user_feature_overrides",
                columns: new[] { "UserId", "FeatureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_UserId_CreatedAt",
                table: "user_notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_UserId_ReferenceKey",
                table: "user_notifications",
                columns: new[] { "UserId", "ReferenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_onboarding_UserId",
                table: "user_onboarding",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_UserId",
                table: "user_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_ExternalSubscriptionId",
                table: "user_subscriptions",
                column: "ExternalSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId",
                table: "user_subscriptions",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Document",
                table: "users",
                column: "Document",
                unique: true,
                filter: "\"Document\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_transactions");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "billing_checkouts");

            migrationBuilder.DropTable(
                name: "billing_webhook_events");

            migrationBuilder.DropTable(
                name: "goal_contributions");

            migrationBuilder.DropTable(
                name: "goal_occurrences");

            migrationBuilder.DropTable(
                name: "goal_scopes");

            migrationBuilder.DropTable(
                name: "investment_allocation_targets");

            migrationBuilder.DropTable(
                name: "investment_goals");

            migrationBuilder.DropTable(
                name: "investment_movements");

            migrationBuilder.DropTable(
                name: "loan_installments");

            migrationBuilder.DropTable(
                name: "money_payments");

            migrationBuilder.DropTable(
                name: "monthly_budget_items");

            migrationBuilder.DropTable(
                name: "monthly_financial_snapshots");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "robot_execution_logs");

            migrationBuilder.DropTable(
                name: "robot_settings");

            migrationBuilder.DropTable(
                name: "spaces");

            migrationBuilder.DropTable(
                name: "user_categorization_feedback");

            migrationBuilder.DropTable(
                name: "user_feature_overrides");

            migrationBuilder.DropTable(
                name: "user_notifications");

            migrationBuilder.DropTable(
                name: "user_onboarding");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "goals");

            migrationBuilder.DropTable(
                name: "investment_positions");

            migrationBuilder.DropTable(
                name: "loan_contracts");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "money_installments");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "institutions");

            migrationBuilder.DropTable(
                name: "money_plans");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "card_brands");
        }
    }
}
