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
