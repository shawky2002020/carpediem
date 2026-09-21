IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    IF SCHEMA_ID(N'audit') IS NULL EXEC(N'CREATE SCHEMA [audit];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    IF SCHEMA_ID(N'events') IS NULL EXEC(N'CREATE SCHEMA [events];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [identity].[Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [identity].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [identity].[RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [audit].[AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [Action] varchar(100) NOT NULL,
        [ResourceType] varchar(50) NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        [MandatoryReason] nvarchar(1000) NULL,
        [Source] varchar(50) NOT NULL,
        [PreviousValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        CONSTRAINT [PK_audit_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users] FOREIGN KEY ([ActorUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [events].[Events] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [StartDateTime] datetimeoffset NOT NULL,
        [EndDateTime] datetimeoffset NOT NULL,
        [Location] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [Status] varchar(20) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_events_Events] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Events_EndUtc] CHECK (DATEPART(TZOFFSET, [EndDateTime]) = 0),
        CONSTRAINT [CK_Events_LocationNonblank] CHECK (LEN(LTRIM(RTRIM([Location]))) > 0),
        CONSTRAINT [CK_Events_StartUtc] CHECK (DATEPART(TZOFFSET, [StartDateTime]) = 0),
        CONSTRAINT [CK_Events_Status] CHECK ([Status] IN ('Draft', 'Published', 'Completed', 'Cancelled')),
        CONSTRAINT [CK_Events_TimeWindow] CHECK ([EndDateTime] > [StartDateTime]),
        CONSTRAINT [CK_Events_TitleNonblank] CHECK (LEN(LTRIM(RTRIM([Title]))) > 0),
        CONSTRAINT [FK_Events_Users_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [identity].[UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [identity].[UserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [identity].[UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [identity].[UserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [events].[DraftCreateRequests] (
        [ActorUserId] uniqueidentifier NOT NULL,
        [RequestToken] uniqueidentifier NOT NULL,
        [PayloadFingerprint] binary(32) NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_events_DraftCreateRequests] PRIMARY KEY ([ActorUserId], [RequestToken]),
        CONSTRAINT [FK_DraftCreateRequests_Events] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
        CONSTRAINT [FK_DraftCreateRequests_Users] FOREIGN KEY ([ActorUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE TABLE [events].[StaffingCycles] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [CycleNumber] int NOT NULL,
        [EffectiveEventDate] date NOT NULL,
        [IsCurrent] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_events_StaffingCycles] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StaffingCycles_Number] CHECK ([CycleNumber] >= 1),
        CONSTRAINT [FK_StaffingCycles_Events] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [audit].[AuditLogs] ([Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorUserId] ON [audit].[AuditLogs] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CorrelationId] ON [audit].[AuditLogs] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OccurredAt] ON [audit].[AuditLogs] ([OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Resource] ON [audit].[AuditLogs] ([ResourceType], [ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_DraftCreateRequests_EventId] ON [events].[DraftCreateRequests] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_Events_CreatedByUserId] ON [events].[Events] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_RoleClaims_RoleId] ON [identity].[RoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [identity].[Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_StaffingCycles_CurrentPerEvent] ON [events].[StaffingCycles] ([EventId]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE UNIQUE INDEX [UX_StaffingCycles_Event_CycleNumber] ON [events].[StaffingCycles] ([EventId], [CycleNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [identity].[UserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [identity].[UserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [identity].[UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [identity].[Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [identity].[Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

COMMIT;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918161227_InitialDraftEvent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918161227_InitialDraftEvent', N'10.0.12');
END;
GO
