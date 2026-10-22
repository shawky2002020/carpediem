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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    IF SCHEMA_ID(N'notifications') IS NULL EXEC(N'CREATE SCHEMA [notifications];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE TABLE [events].[ChiefLeaderAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [ChiefLeaderUserId] uniqueidentifier NOT NULL,
        [Status] varchar(20) NOT NULL,
        [AppointedByUserId] uniqueidentifier NOT NULL,
        [AppointedAt] datetimeoffset NOT NULL,
        [AcceptedAt] datetimeoffset NULL,
        [AcceptanceSource] varchar(20) NULL,
        [AcceptedByUserId] uniqueidentifier NULL,
        [DeclinedAt] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_events_ChiefLeaderAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ChiefLeaderAssignments_Source] CHECK ([AcceptanceSource] IS NULL OR [AcceptanceSource] IN ('Direct','AdminExternal')),
        CONSTRAINT [CK_ChiefLeaderAssignments_Status] CHECK ([Status] IN ('Pending','Active','Declined')),
        CONSTRAINT [FK_ChiefLeaderAssignments_AcceptedBy] FOREIGN KEY ([AcceptedByUserId]) REFERENCES [identity].[Users] ([Id]),
        CONSTRAINT [FK_ChiefLeaderAssignments_AppointedBy] FOREIGN KEY ([AppointedByUserId]) REFERENCES [identity].[Users] ([Id]),
        CONSTRAINT [FK_ChiefLeaderAssignments_Events] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
        CONSTRAINT [FK_ChiefLeaderAssignments_Users] FOREIGN KEY ([ChiefLeaderUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE TABLE [notifications].[InPlatformNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [RecipientUserId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NULL,
        [NotificationType] varchar(50) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        [ContextPayload] nvarchar(500) NULL,
        CONSTRAINT [PK_notifications_InPlatformNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Notifications_ReadAt] CHECK ([ReadAt] IS NULL OR [ReadAt] >= [CreatedAt]),
        CONSTRAINT [FK_Notifications_Events] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
        CONSTRAINT [FK_Notifications_Users] FOREIGN KEY ([RecipientUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE TABLE [events].[ChiefLeaderAppointmentRequests] (
        [ActorUserId] uniqueidentifier NOT NULL,
        [RequestToken] uniqueidentifier NOT NULL,
        [PayloadFingerprint] binary(32) NOT NULL,
        [AssignmentId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_events_ChiefLeaderAppointmentRequests] PRIMARY KEY ([ActorUserId], [RequestToken]),
        CONSTRAINT [FK_ChiefLeaderAppointmentRequests_Assignments] FOREIGN KEY ([AssignmentId]) REFERENCES [events].[ChiefLeaderAssignments] ([Id]),
        CONSTRAINT [FK_ChiefLeaderAppointmentRequests_Users] FOREIGN KEY ([ActorUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE INDEX [IX_ChiefLeaderAppointmentRequests_AssignmentId] ON [events].[ChiefLeaderAppointmentRequests] ([AssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE INDEX [IX_ChiefLeaderAssignments_AcceptedByUserId] ON [events].[ChiefLeaderAssignments] ([AcceptedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE INDEX [IX_ChiefLeaderAssignments_AppointedByUserId] ON [events].[ChiefLeaderAssignments] ([AppointedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE INDEX [IX_ChiefLeaderAssignments_ChiefLeaderUserId] ON [events].[ChiefLeaderAssignments] ([ChiefLeaderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ChiefLeaderAssignments_ActivePerEvent] ON [events].[ChiefLeaderAssignments] ([EventId]) WHERE [Status] = ''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_ChiefLeaderAssignments_PendingPerEvent] ON [events].[ChiefLeaderAssignments] ([EventId]) WHERE [Status] = 'Pending';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE INDEX [IX_InPlatformNotifications_EventId] ON [notifications].[InPlatformNotifications] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    CREATE INDEX [IX_Notifications_Recipient_CreatedAt] ON [notifications].[InPlatformNotifications] ([RecipientUserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922063003_ChiefLeaderAppointment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260922063003_ChiefLeaderAppointment', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001181651_AddAvailabilityResponses'
)
BEGIN
    IF SCHEMA_ID(N'staffing') IS NULL EXEC(N'CREATE SCHEMA [staffing];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001181651_AddAvailabilityResponses'
)
BEGIN
    CREATE TABLE [staffing].[AvailabilityResponses] (
        [Id] uniqueidentifier NOT NULL,
        [StaffingCycleId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Response] nvarchar(32) NOT NULL,
        [DeclaredAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_staffing_AvailabilityResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AvailabilityResponses_Events] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
        CONSTRAINT [FK_AvailabilityResponses_StaffingCycles] FOREIGN KEY ([StaffingCycleId]) REFERENCES [events].[StaffingCycles] ([Id]),
        CONSTRAINT [FK_AvailabilityResponses_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001181651_AddAvailabilityResponses'
)
BEGIN
    CREATE INDEX [IX_AvailabilityResponses_Event_Response] ON [staffing].[AvailabilityResponses] ([EventId], [Response]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001181651_AddAvailabilityResponses'
)
BEGIN
    CREATE INDEX [IX_AvailabilityResponses_UserId] ON [staffing].[AvailabilityResponses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001181651_AddAvailabilityResponses'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AvailabilityResponses_StaffingCycle_User] ON [staffing].[AvailabilityResponses] ([StaffingCycleId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001181651_AddAvailabilityResponses'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001181651_AddAvailabilityResponses', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001195134_AddTeamAssignments'
)
BEGIN
    CREATE TABLE [staffing].[TeamAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [StaffingCycleId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [AssignedAt] datetimeoffset NOT NULL,
        [AssignedByUserId] uniqueidentifier NOT NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CancelledAt] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_staffing_TeamAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TeamAssignments_Status] CHECK ([Status] IN ('Active','Cancelled')),
        CONSTRAINT [FK_TeamAssignments_AssignedByUsers] FOREIGN KEY ([AssignedByUserId]) REFERENCES [identity].[Users] ([Id]),
        CONSTRAINT [FK_TeamAssignments_Events] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
        CONSTRAINT [FK_TeamAssignments_StaffingCycles] FOREIGN KEY ([StaffingCycleId]) REFERENCES [events].[StaffingCycles] ([Id]),
        CONSTRAINT [FK_TeamAssignments_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001195134_AddTeamAssignments'
)
BEGIN
    CREATE INDEX [IX_TeamAssignments_AssignedByUserId] ON [staffing].[TeamAssignments] ([AssignedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001195134_AddTeamAssignments'
)
BEGIN
    CREATE INDEX [IX_TeamAssignments_Event_Status] ON [staffing].[TeamAssignments] ([EventId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001195134_AddTeamAssignments'
)
BEGIN
    CREATE INDEX [IX_TeamAssignments_UserId] ON [staffing].[TeamAssignments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001195134_AddTeamAssignments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TeamAssignments_ActiveLeaderPerCycle] ON [staffing].[TeamAssignments] ([StaffingCycleId], [UserId]) WHERE [Status] = ''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001195134_AddTeamAssignments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001195134_AddTeamAssignments', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002105744_AddAttendanceOutcomes'
)
BEGIN
    IF SCHEMA_ID(N'attendance') IS NULL EXEC(N'CREATE SCHEMA [attendance];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002105744_AddAttendanceOutcomes'
)
BEGIN
    CREATE TABLE [attendance].[AttendanceOutcomes] (
        [Id] uniqueidentifier NOT NULL,
        [TeamAssignmentId] uniqueidentifier NOT NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [FinalizedByUserId] uniqueidentifier NOT NULL,
        [FinalizedAt] datetimeoffset NOT NULL,
        [CorrectedByUserId] uniqueidentifier NULL,
        [CorrectedAt] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_attendance_AttendanceOutcomes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AttendanceOutcomes_Outcome] CHECK ([Outcome] IN ('Attended','NoShow')),
        CONSTRAINT [FK_AttendanceOutcomes_CorrectedByUsers] FOREIGN KEY ([CorrectedByUserId]) REFERENCES [identity].[Users] ([Id]),
        CONSTRAINT [FK_AttendanceOutcomes_FinalizedByUsers] FOREIGN KEY ([FinalizedByUserId]) REFERENCES [identity].[Users] ([Id]),
        CONSTRAINT [FK_AttendanceOutcomes_TeamAssignments] FOREIGN KEY ([TeamAssignmentId]) REFERENCES [staffing].[TeamAssignments] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002105744_AddAttendanceOutcomes'
)
BEGIN
    CREATE INDEX [IX_AttendanceOutcomes_CorrectedByUserId] ON [attendance].[AttendanceOutcomes] ([CorrectedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002105744_AddAttendanceOutcomes'
)
BEGIN
    CREATE INDEX [IX_AttendanceOutcomes_FinalizedByUserId] ON [attendance].[AttendanceOutcomes] ([FinalizedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002105744_AddAttendanceOutcomes'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AttendanceOutcomes_Assignment] ON [attendance].[AttendanceOutcomes] ([TeamAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002105744_AddAttendanceOutcomes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002105744_AddAttendanceOutcomes', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    IF SCHEMA_ID(N'engagement') IS NULL EXEC(N'CREATE SCHEMA [engagement];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE TABLE [engagement].[EventFeedback] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [RaterUserId] uniqueidentifier NOT NULL,
        [LeaderUserId] uniqueidentifier NULL,
        [Kind] nvarchar(16) NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(500) NULL,
        [SubmittedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EventFeedback] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EventFeedback_Kind] CHECK ([Kind] IN ('Event','Leader')),
        CONSTRAINT [CK_EventFeedback_Rating] CHECK ([Rating] BETWEEN 1 AND 5),
        CONSTRAINT [CK_EventFeedback_Subject] CHECK (([Kind] = 'Event' AND [LeaderUserId] IS NULL) OR ([Kind] = 'Leader' AND [LeaderUserId] IS NOT NULL)),
        CONSTRAINT [FK_EventFeedback_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
        CONSTRAINT [FK_EventFeedback_Users_LeaderUserId] FOREIGN KEY ([LeaderUserId]) REFERENCES [identity].[Users] ([Id]),
        CONSTRAINT [FK_EventFeedback_Users_RaterUserId] FOREIGN KEY ([RaterUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE TABLE [engagement].[IncidentReports] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [ReporterUserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        [ReportedAt] datetimeoffset NOT NULL,
        [ReviewedAt] datetimeoffset NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewNote] nvarchar(1000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_IncidentReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncidentReports_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
        CONSTRAINT [FK_IncidentReports_Users_ReporterUserId] FOREIGN KEY ([ReporterUserId]) REFERENCES [identity].[Users] ([Id]),
        CONSTRAINT [FK_IncidentReports_Users_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [identity].[Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EventFeedback_EventId_RaterUserId_Kind_LeaderUserId] ON [engagement].[EventFeedback] ([EventId], [RaterUserId], [Kind], [LeaderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE INDEX [IX_EventFeedback_LeaderUserId] ON [engagement].[EventFeedback] ([LeaderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE INDEX [IX_EventFeedback_RaterUserId] ON [engagement].[EventFeedback] ([RaterUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE INDEX [IX_IncidentReports_EventId_ReportedAt] ON [engagement].[IncidentReports] ([EventId], [ReportedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE INDEX [IX_IncidentReports_ReporterUserId] ON [engagement].[IncidentReports] ([ReporterUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE INDEX [IX_IncidentReports_ReviewedAt] ON [engagement].[IncidentReports] ([ReviewedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    CREATE INDEX [IX_IncidentReports_ReviewedByUserId] ON [engagement].[IncidentReports] ([ReviewedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022160531_AddFeedbackAndIncidents'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022160531_AddFeedbackAndIncidents', N'10.0.12');
END;

COMMIT;
GO
