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

