## SyncRoo
SyncRoo can quickly synchronize files between devices. It's a lot quicker than Robocopy in subsequent delta syncs.

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

Robocopy took over 4 days to work out the delta and started to copy the first file.

SyncRoo took 30 minutes to find the delta file list, which is over 200 times faster than Robocopy.

## Usage

## Command Options

```text
  -s, --Source          Required. The source folder where the files to be copied from.

  -t, --Target          Required. The target folder where the files to be copied to.

  -b, --Batch           The intermediate folder for the file copy batch commands to be stored.

  -f, --FilePatterns    The file patterns to be serched for.

  -o, --Operation       A specific operation to be run rather than the whole sync process.

  -m, --MultiThreads    (Default: 1) The number of threads the process will use to concurrenctly copy the files.

  -d, --Database        The database connection string.

  -a, --AutoTeardown    (Default: false) Automatically teardown intermediate resources.

  -n, --UsnJournal      (Default: false) Use NTFS USN Journal to quickly search for files but this may use large volume of memory depending on the number of files on the drives.

  -p, --Profile         (Default: false) A profile file where you can define a series of source/target folders to be synced repeatedly.

  --help                Display this help screen.

  --version             Display version information.
```

### Basic Commands

If it's for occasional file sync, you may choose to specify the source folder with `-s` parameter and the target folder with `-t` parameter, like below:
```bat
SyncRoo -s "D:\MyPictures\Favorites" -t "Z:\Backup\Pictures\Favorites"
```

By default, it would use the `Batch` folder in the SyncRoo application folder to store intermediate batch files that will be executed to synchronize the files. You can specify a different folder if needed, like below:
```bat
SyncRoo -s "D:\MyPictures\Favorites" -t "Z:\Backup\Pictures\Favorites" -b "C:\Temp\SyncRooBatch"
```

You can specify the file patterns if it's not for all the files (by default it's *.*) via the `-f` parameter, like below:
```bat
SyncRoo -s "D:\MyPictures\Favorites" -t "Z:\Backup\Pictures\Favorites" -b "C:\Temp\SyncRooBatch", -f "*.jpg" "*.png" "XYZ*.tiff"
```

### Profile Command
If you need to regularly synchronize files between folders, you may create a profile file with multiple sync tasks, which is a simple json file, like below:
```json
{
	"tasks": [
		{
			"sourceFolder": "D:\\MyPictures\\Favorites",
			"targetFolder": "X:\\Backup\\Pictures\\Favorites",
			"filePatterns": [
				"*.jpg",
				"*.png",
				"XYZ*.tiff"
			]
		},
		{
			"sourceFolder": "D:\\MyVideos\\BestCollections",
			"targetFolder": "Y:\\AnotherBackup\\Videos\\BestCollections"
		},
		{
			"sourceFolder": "D:\\MyMusics\\TopAlbums",
			"targetFolder": "Z:\\MoreBackup\\Music\\TopAlbums"
		}
	]
}
```

Then you can provide the profile file via `-p` parameter, like below:
```bat
SyncRoo -p "D:\MySyncRooTasks\DailySync.json"
```

### NTFS USN Journal Command
NTFS tracks changes to the file system and store the info on the MFT. We can quickly search matching files on an NTFS USN Journal enabled drive.

> [!NOTE]
> Only fixed drive with NTFS file system enabled supports this feature. Mapped network drives and UNC paths do not support this.

> [!IMPORTANT]
> You will need to run SyncRoo with elevated access (aka. run as administrator), otherwise it will not work.

> [!WARNING]
> SyncRoo will need to preload all the files on the fixed drives so that the result can be shared within all subsequent searches, and that can take some time, and potentially a lot of memory.

To enable the support for USN Journal, specify the `-n` parameter, like below:
```bat
SyncRoo -s "D:\MyPictures\Favorites" -t "Z:\Backup\Pictures\Favorites" -n
```

## Tech Stack
It's primary .NET stack:
- .NET 8 with C# 12
- Dapper: for data access
- FastMember: for fast POCO to database for SQL Server to do bulk insert
- Serilog: logging, currently only log to the console
- CommandLineParser: command line options
- Microsoft.Data.Sqlite: ADO.NET driver for Sqlite
- NTFS USN Journal: credit goes to [EverythingSZ](https://github.com/yuanrui/EverythingSZ)

## Storage Providers
SyncRoo supports multiple storage to persist the file name, size and modified time of the files in the source folder, target folder and the delta, including:
- Sqlite
- SQL Server
- SQL Server Express LocalDB

### Sqlite
Sqlite is shipped with SyncRoo, and it's the default storage provider.

### SQL Server
If you have an existing SQL Server, you can use that and specify the connection string in the appsettings.json file.

If you would like to use a free version, you could download the latest version [SQL Server Express here](https://www.microsoft.com/en-au/sql-server/sql-server-downloads)

### SQL Server Express LocalDB
If you want to use SQL Server Express LocalDB, which is free to use, you will need to bear in mind the database size limit is 10GB.

If the total number of files including the source, the target and the delta are large, for example, over 50M, then it's highly likely it will exceed the 10GB size limit.

To download the latest version (currently 2022), please click [SQL Server 2022 Express LocalDB](https://download.microsoft.com/download/3/8/d/38de7036-2433-4207-8eae-06e247e17b25/SqlLocalDB.msi).

To isntall, follow the steps of the installation wizard.

## ToDos
- Add support for file patterns
