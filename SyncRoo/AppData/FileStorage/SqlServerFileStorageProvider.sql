IF NOT EXISTS (SELECT NULL FROM sys.types t INNER JOIN sys.schemas s on (t.schema_id = s.schema_id) where s.name = 'dbo' and t.name = 'FileType')
BEGIN
	CREATE TYPE [dbo].[FileType] AS TABLE(
		[FileName] [nvarchar](450) NOT NULL,
		[Size] [bigint] NOT NULL,
		[ModifiedTime] [datetime2](7) NOT NULL
	)
END
GO

IF NOT EXISTS (SELECT NULL FROM sys.tables t INNER JOIN sys.schemas s on (t.schema_id = s.schema_id) where s.name = 'dbo' and t.name = 'PendingFile')
BEGIN
CREATE TABLE [dbo].[PendingFile](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[FileName] [nvarchar](450) NOT NULL,
	[Size] [bigint] NOT NULL,
	[ModifiedTime] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)
)
END
GO

IF NOT EXISTS (SELECT NULL FROM sys.tables t INNER JOIN sys.schemas s on (t.schema_id = s.schema_id) where s.name = 'dbo' and t.name = 'SourceFile')
BEGIN
CREATE TABLE [dbo].[SourceFile](
	[FileName] [nvarchar](450) NOT NULL,
	[Size] [bigint] NOT NULL,
	[ModifiedTime] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_SourceFile] PRIMARY KEY CLUSTERED 
(
	[FileName] ASC
)
) ON [PRIMARY]
END
GO

IF NOT EXISTS (SELECT NULL FROM sys.tables t INNER JOIN sys.schemas s on (t.schema_id = s.schema_id) where s.name = 'dbo' and t.name = 'TargetFile')
BEGIN
CREATE TABLE [dbo].[TargetFile](
	[FileName] [nvarchar](450) NOT NULL,
	[Size] [bigint] NOT NULL,
	[ModifiedTime] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_TargetFile] PRIMARY KEY CLUSTERED 
(
	[FileName] ASC
)
) ON [PRIMARY]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_AddPendingFiles]
AS
BEGIN

	INSERT INTO dbo.PendingFile (FileName, Size, ModifiedTime)
		SELECT sf.FileName, sf.Size, sf.ModifiedTime FROM dbo.SourceFile sf
			LEFT OUTER JOIN dbo.TargetFile tf ON sf.FileName = tf.FileName
			WHERE tf.FileName IS NULL OR sf.Size <> tf.Size OR sf.ModifiedTime > tf.ModifiedTime

END
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_AddSourceFiles]
	@Files dbo.FileType READONLY
AS
BEGIN

    MERGE dbo.SourceFile target
        USING (SELECT FileName, Size, ModifiedTime FROM @Files) AS source
            ON (target.FileName = source.FileName)
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (FileName, Size, ModifiedTime)
                    VALUES (source.FileName, source.Size, source.ModifiedTime)
			WHEN MATCHED THEN
				UPDATE SET target.Size = source.Size, target.ModifiedTime = source.ModifiedTime;

END
GO


CREATE OR ALTER PROCEDURE [dbo].[usp_AddTargetFiles]
	@Files dbo.FileType READONLY
AS
BEGIN

    MERGE dbo.TargetFile target
        USING (SELECT FileName, Size, ModifiedTime FROM @Files) AS source
            ON (target.FileName = source.FileName)
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (FileName, Size, ModifiedTime)
                    VALUES (source.FileName, source.Size, source.ModifiedTime)
			WHEN MATCHED THEN
				UPDATE SET target.Size = source.Size, target.ModifiedTime = source.ModifiedTime;

END
GO
