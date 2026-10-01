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

