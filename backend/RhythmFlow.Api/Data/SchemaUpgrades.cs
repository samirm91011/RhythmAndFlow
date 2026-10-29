using Microsoft.EntityFrameworkCore;

namespace RhythmFlow.Api.Data;

/// <summary>
/// Brings a database that was created by an earlier version up to date. EnsureCreated only builds a brand-new database and
/// never changes an existing one, so without this a server that was deployed earlier would be missing newer tables and columns.
/// Everything here is safe to repeat on every start and does nothing on a fresh database.
/// Use this for small additive changes until EF Core migrations are adopted.
/// </summary>
public static class SchemaUpgrades
{
    /// <summary>
    /// The client approved new category names (the app's Move filter and every lesson use them). Lessons saved under the old names
    /// are renamed here. Safe to repeat: once renamed, nothing matches the old names any more.
    /// </summary>
    private static readonly string[] CategoryRenames =
    [
        """UPDATE "Lessons" SET "Category" = 'Barre & Pilates' WHERE "Category" = 'Barre'""",
        """UPDATE "Lessons" SET "Category" = 'Stretch & Mobility' WHERE "Category" = 'Stretch'""",
    ];

    /// <summary>A column that older databases do not have yet. Type names are given for PostgreSQL and SQLite.</summary>
    private record Column(string Table, string Name, string Postgres, string Sqlite);

    private static readonly Column[] NewColumns =
    [
        new("Plans", "AnnualPrice", "numeric(10,2) NULL", "TEXT NULL"),
        new("Plans", "LaunchPrice", "numeric(10,2) NULL", "TEXT NULL"),
        new("Plans", "LaunchEndsAt", "timestamp with time zone NULL", "TEXT NULL"),
        new("Plans", "TrialDays", "integer NOT NULL DEFAULT 0", "INTEGER NOT NULL DEFAULT 0"),
        new("Subscriptions", "Billing", "text NOT NULL DEFAULT 'MONTHLY'", "TEXT NOT NULL DEFAULT 'MONTHLY'"),
        new("Subscriptions", "InitialAmount", "numeric(10,2) NULL", "TEXT NULL"),
        new("Subscriptions", "TrialDays", "integer NOT NULL DEFAULT 0", "INTEGER NOT NULL DEFAULT 0"),
        new("Subscriptions", "PlannedStart", "timestamp with time zone NULL", "TEXT NULL"),
        new("Subscriptions", "AwaitingFirstCharge", "boolean NOT NULL DEFAULT false", "INTEGER NOT NULL DEFAULT 0"),
        new("Subscriptions", "ReminderForEnd", "timestamp with time zone NULL", "TEXT NULL"),
        new("Subscriptions", "LockedPrice", "numeric(10,2) NULL", "TEXT NULL"),
        // Email verification at sign-up. Everyone who already has an account counts as verified.
        new("Users", "EmailVerified", "boolean NOT NULL DEFAULT true", "INTEGER NOT NULL DEFAULT 1"),
        new("PasswordResets", "Purpose", "text NOT NULL DEFAULT 'RESET'", "TEXT NOT NULL DEFAULT 'RESET'"),
        // Paid events: what an event is, what it costs and how people join it
        new("Classes", "Kind", "text NOT NULL DEFAULT 'CLASS'", "TEXT NOT NULL DEFAULT 'CLASS'"),
        new("Classes", "Format", "text NOT NULL DEFAULT 'IN_PERSON'", "TEXT NOT NULL DEFAULT 'IN_PERSON'"),
        new("Classes", "Price", "numeric(10,2) NOT NULL DEFAULT 0", "TEXT NOT NULL DEFAULT '0'"),
        new("Classes", "PosterUrl", "text NULL", "TEXT NULL"),
        new("Classes", "SalesCloseHours", "integer NOT NULL DEFAULT 24", "INTEGER NOT NULL DEFAULT 24"),
        new("Classes", "SalesOpenAt", "timestamp with time zone NULL", "TEXT NULL"),
        new("Classes", "MemberEarlyHours", "integer NOT NULL DEFAULT 0", "INTEGER NOT NULL DEFAULT 0"),
        new("Classes", "JoinPlatform", "text NULL", "TEXT NULL"),
        new("Classes", "JoinLink", "text NULL", "TEXT NULL"),
        new("Classes", "PracticalNote", "text NULL", "TEXT NULL"),
        new("Classes", "WaitlistEnabled", "boolean NOT NULL DEFAULT false", "INTEGER NOT NULL DEFAULT 0"),
        // Tickets
        new("Bookings", "AmountDue", "numeric(10,2) NOT NULL DEFAULT 0", "TEXT NOT NULL DEFAULT '0'"),
        new("Bookings", "CreditApplied", "numeric(10,2) NOT NULL DEFAULT 0", "TEXT NOT NULL DEFAULT '0'"),
        new("Bookings", "CreditUses", "text NULL", "TEXT NULL"),
        new("Bookings", "AmountPaid", "numeric(10,2) NOT NULL DEFAULT 0", "TEXT NOT NULL DEFAULT '0'"),
        new("Bookings", "PayFastPaymentId", "text NULL", "TEXT NULL"),
        new("Bookings", "HoldUntil", "timestamp with time zone NULL", "TEXT NULL"),
        new("Bookings", "ChoiceNeeded", "text NULL", "TEXT NULL"),
        new("Bookings", "RefundStatus", "text NULL", "TEXT NULL"),
        new("Bookings", "RefundAmount", "numeric(10,2) NOT NULL DEFAULT 0", "TEXT NOT NULL DEFAULT '0'"),
        new("Bookings", "RequestStatus", "text NULL", "TEXT NULL"),
        new("Bookings", "RequestChoice", "text NULL", "TEXT NULL"),
        new("Bookings", "RequestedAt", "timestamp with time zone NULL", "TEXT NULL"),
        new("Bookings", "LinkSentAt", "timestamp with time zone NULL", "TEXT NULL"),
        new("Bookings", "DayReminderSent", "boolean NOT NULL DEFAULT false", "INTEGER NOT NULL DEFAULT 0"),
        new("Bookings", "HourReminderSent", "boolean NOT NULL DEFAULT false", "INTEGER NOT NULL DEFAULT 0"),
    ];

    public static async Task ApplyAsync(AppDbContext db)
    {
        string[] statements;
        if (db.Database.IsNpgsql())
        {
            statements =
            [
                """
                CREATE TABLE IF NOT EXISTS "DeviceTokens" (
                    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    "UserId" uuid NOT NULL,
                    "Token" character varying(512) NOT NULL,
                    "Platform" character varying(20) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "LastSeen" timestamp with time zone NOT NULL
                )
                """,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeviceTokens_Token" ON "DeviceTokens" ("Token")""",
                """CREATE INDEX IF NOT EXISTS "IX_DeviceTokens_UserId" ON "DeviceTokens" ("UserId")""",
                """
                CREATE TABLE IF NOT EXISTS "Credits" (
                    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    "UserId" uuid NOT NULL,
                    "Amount" numeric(10,2) NOT NULL,
                    "Remaining" numeric(10,2) NOT NULL,
                    "IssuedAt" timestamp with time zone NOT NULL,
                    "ExpiresAt" timestamp with time zone NOT NULL,
                    "Reason" text NOT NULL,
                    "BookingId" integer NULL
                )
                """,
                """CREATE INDEX IF NOT EXISTS "IX_Credits_UserId" ON "Credits" ("UserId")""",
                """
                CREATE TABLE IF NOT EXISTS "AppContents" (
                    "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    "Key" character varying(64) NOT NULL,
                    "Json" text NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                )
                """,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppContents_Key" ON "AppContents" ("Key")""",
                .. NewColumns.Select(c => $"""ALTER TABLE "{c.Table}" ADD COLUMN IF NOT EXISTS "{c.Name}" {c.Postgres}"""),
                .. CategoryRenames,
            ];
            foreach (var sql in statements) await db.Database.ExecuteSqlRawAsync(sql);
        }
        else if (db.Database.IsSqlite())
        {
            statements =
            [
                """
                CREATE TABLE IF NOT EXISTS "DeviceTokens" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_DeviceTokens" PRIMARY KEY AUTOINCREMENT,
                    "UserId" TEXT NOT NULL,
                    "Token" TEXT NOT NULL,
                    "Platform" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "LastSeen" TEXT NOT NULL
                )
                """,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeviceTokens_Token" ON "DeviceTokens" ("Token")""",
                """CREATE INDEX IF NOT EXISTS "IX_DeviceTokens_UserId" ON "DeviceTokens" ("UserId")""",
                """
                CREATE TABLE IF NOT EXISTS "Credits" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Credits" PRIMARY KEY AUTOINCREMENT,
                    "UserId" TEXT NOT NULL,
                    "Amount" TEXT NOT NULL,
                    "Remaining" TEXT NOT NULL,
                    "IssuedAt" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "Reason" TEXT NOT NULL,
                    "BookingId" INTEGER NULL
                )
                """,
                """CREATE INDEX IF NOT EXISTS "IX_Credits_UserId" ON "Credits" ("UserId")""",
                """
                CREATE TABLE IF NOT EXISTS "AppContents" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_AppContents" PRIMARY KEY AUTOINCREMENT,
                    "Key" TEXT NOT NULL,
                    "Json" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                )
                """,
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppContents_Key" ON "AppContents" ("Key")""",
            ];
            foreach (var sql in statements) await db.Database.ExecuteSqlRawAsync(sql);
            // SQLite has no "ADD COLUMN IF NOT EXISTS", so look first.
            foreach (var c in NewColumns)
            {
                var exists = await db.Database
                    .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM pragma_table_info({c.Table}) WHERE name = {c.Name}")
                    .FirstAsync();
                if (exists == 0) await db.Database.ExecuteSqlRawAsync($"""ALTER TABLE "{c.Table}" ADD COLUMN "{c.Name}" {c.Sqlite}""");
            }
            foreach (var sql in CategoryRenames) await db.Database.ExecuteSqlRawAsync(sql);
        }
        else return;

        await ApplyClientPlanCopyAsync(db);
        await ApplyLessonLevelsAsync(db);
    }

    /// <summary>
    /// The client approved three levels: Beginner, Intermediate and Advanced. Lessons saved before that say "All levels", so they
    /// become Beginner, except the two longer workouts from her channel, which are Intermediate. Safe to repeat.
    /// </summary>
    private static async Task ApplyLessonLevelsAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Lessons" SET "Level" = 'Intermediate' WHERE "Level" = 'All levels' AND "VideoReference" IN ('JfLKk17S8KQ', 'Y3ZCfwIV3js')""");
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Lessons" SET "Level" = 'Beginner' WHERE "Level" = 'All levels'""");
    }

    /// <summary>
    /// A database made before the client sent her plans still holds the three placeholder plans and programmes. This turns them
    /// into her two memberships and her structured programme, matched by the old placeholder names so anything an administrator
    /// has already renamed or edited is left alone. Safe to repeat: after the first run nothing matches the old names.
    /// </summary>
    private static async Task ApplyClientPlanCopyAsync(AppDbContext db)
    {
        var lib = ClientPlans.Library;
        var guided = ClientPlans.Guided;
        var months = ClientPlans.AnnualMonthsCharged;
        var libAnnual = lib.Price * months;
        var guidedAnnual = guided.Price * months;

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Plans" SET "Name" = {lib.Name}, "Price" = {lib.Price}, "Description" = {lib.Description}, "Features" = {lib.Features},
                "Tier" = 1, "Status" = 'ACTIVE', "AnnualPrice" = {libAnnual}
            WHERE "Name" = 'Flow'
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Plans" SET "Name" = {guided.Name}, "Price" = {guided.Price}, "Description" = {guided.Description}, "Features" = {guided.Features},
                "Tier" = 2, "Status" = 'ACTIVE', "AnnualPrice" = {guidedAnnual}
            WHERE "Name" = 'Rhythm'
            """);
        // The third placeholder plan is hidden, not deleted: anyone who holds it keeps it and payments still refer to it.
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Plans" SET "Status" = 'INACTIVE' WHERE "Name" = 'Rhythm & Flow Unlimited'""");

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Programmes" SET "Name" = '7-Day Flow Plan', "Description" = 'A structured week of movement, breathing and rest.', "MinTier" = 2
            WHERE "Name" = 'Full Body Flow Masterclass'
            """);
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Programmes" SET "MinTier" = 1 WHERE "Name" = 'Mindful Mobility' AND "MinTier" = 2""");
    }
}
