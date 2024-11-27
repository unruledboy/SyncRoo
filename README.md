## SyncRoo
SyncRoo can quickly synchornize files between devices. It's a lot quicker than RoboCopy in subsequent delta syncs.

Currently it only supports Windows devices.

## Rationale
It will first get the full list of file names, alongside with the file size and last modified time of the source folder, and save it to a database table.

Then it will do the same for the target/destination folder.

Then it will compare the two, and find out the differences (delta), wehther a file is new or modified (in terms of change of the size or modification time).

Then it will produce a series of BAT files containing the files to be copied from the source folder to the target folder.

## Performance
When copying large amount of files, it can be both IO bound, and when it comes to delta sync, it will be first CPU bound then IO bound.

The number of test sample files is 14 million. The source folder have 14 million files, the target folder has about 13 million files.

The hardware is i5-13600K CPU + AData Legend 800 3.5GB/s SSD.

RoboCopy took over 4 days to work out the delta and started to copy the first file.

SyncRoo took 30 minutes to find the delta file list, which is over 200 times faster than RoboCopy.

## Tech stack
It's primary .NET stack:
- .NET 8 with C# 12
- Dapper: for data access
- FastMember: for fast POCO to database for SQL Server to do bulk insert
- Serilog: logging, currently only log to the console
- CommandLineParser: command line options

## Storage Providers
SyncRoo supports multiple storage to persist the file name, size and modified time of the files in the source folder, target folder and the delta, including:
- SQL Server
- SQL Server Express LocalDB
- Sqlite

### SQL Server
If you have an existing SQL Server, you can use that and specify the connection string in the appsettings.json file.

If you would like to use a free version, you could download the latest version [SQL Server Express here](https://www.microsoft.com/en-au/sql-server/sql-server-downloads)

### SQL Server Express LocalDB
If you want to use SQL Server Express LocalDB, which is free to use, you will need to bear in mind the database size limit is 10GB.

If the total number of files including the source, the target and the delta are large, for example, over 50M, then it's highly likely it will exceed the 10GB size limit.

To download the latest version (currently 2022), please click [SQL Server 2022 Express LocalDB](https://download.microsoft.com/download/3/8/d/38de7036-2433-4207-8eae-06e247e17b25/SqlLocalDB.msi).

To isntall, follow the steps of the installation wizard.


### Sqlite
Sqlite is shipped with SyncRoo.
