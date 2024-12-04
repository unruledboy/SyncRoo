## SyncRoo
SyncRoo can quickly synchronize files between devices. It's a lot quicker than Robocopy in subsequent delta syncs.

Currently it only supports Windows devices.

## Features
SyncRoo is created for my personal usage as I was not happy with the formance and functionalities of other solutions.

In one of my other project, I have over 30 million files that need to be synchornized between devices regularly. And I need that to run fast and effectively, only synchornize the new / changed files.

And whatever I am after, have been implemented in SyncRoo:
- Fast and with low memory consumption: SyncRoo is really fast, by leveraging the power of built-in Sqlite / external database systems, which can efficiently manage and compare the files that need to be synchornized.
- Cross machine sync: traditionally we may use UNC path, or mapped network drive, they are all transferred over SMB protocol, and the performance is not good. SyncRoo comes with dedicated server instance in each machine, and leverage the native API to gain maximum performance, then send the result over network. The performance is a lot better.
- Custom profiles: rather than typing the same command parameters every time you need to sychronize files between folders / machines, you can create a JSON-based profile file and reuse it in the automation tools like Windows Task Scheduler.
- Native Windows NTFS Journal support: NTFS tracks changes to the file system and store the info on the MFT. We can quickly search matching files on an NTFS USN Journal enabled drive.

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

  -m, --MultiThreads    (Default: 5) The number of threads the process will use to concurrenctly copy the files.

  -d, --Database        The database connection string.

  -r, --Rule            (Default: standard) The rule to determine whether a file should be copied or not: standard, newer, larger.

  -l, --Limits          The limits to determine whether a file should be copied or not: min/max size, min/max date.

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
			"targetFolder": "Y:\\AnotherBackup\\Videos\\BestCollections",
			"isEnabled": false
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

If you would like to temporarily disable certain task in the profile, you could set `isEnabled` to be `false` for the task, as shown in the second task in above sample.

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

### Rules
There are 3 types of rules:
- Standard: this is the default rule, where a new file or a file is different in size or last modified time.
- Newer: only copy the file if it's newer in the source folder
- Larger: only copy the file if it's larger in the soruce folder

For example, if you want to only copy the newer files from the source folder with last modified date being later than the one in the target folder, then you can specify the `-r` parameter, like below:
```bat
SyncRoo -s "D:\MyPictures\Favorites" -t "Z:\Backup\Pictures\Favorites" -r newer
```

### Limits
By default there is no limit, all the files will be included: either a new file, or a file with last modified time changed or the size is different.

There are 4 types of limits to decide whether a file should be copied or not:
- SizeMin
- SizeMax
- DateMin
- DateMax

For example, if you want to only copy the files with the size being less than 1GB, then you can specify the `-r` parameter, like below:

```bat
SyncRoo -s "D:\MyPictures\Favorites" -t "Z:\Backup\Pictures\Favorites" -l "SizeMax=1GB"
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
| Provide Name  | Provider Code |
| ------------- | ------------- |
| Sqlite  | Sqlite  |
| In-Memory  | InMemory  |
| SQL Server  | SQLServer  |
| SQL Server Express LocalDB | SQLServer  |

To use them, please set the following two values based on the supported provider in the `appsettings.json` or `appsettings.release.json` file which sits at the root folder of SyncRoo:
- ConnectionStrings > Database
- Sync -> FilStorageProvider

The value of FilStorageProvider is the `Provider Code` in the aforementioned table.

### Sqlite
Sqlite is shipped with SyncRoo, and it's the default storage provider.

For example, to use Sqlite, you can set like below:
```json
{
  "ConnectionStrings": {
    "Database": "Data Source=D:\SyncRoo-Data\\SyncRoo-Sqlite.db"
  },
  "Sync": {
    "FilStorageProvider": "Sqlite"
  }
}
```

### In-Memory
The in-memory provider is the fastest, but it may consume large amount of the memory depending on the number of the files being processed.

> [!WARNING]
> SyncRoo will need to hold the meta data of the files (file name, size and last modified time), and that could be a lot of memory usage.

For example, to use in-memory, you can set like below:

```json
{
  "ConnectionStrings": {
    "Database": ""
  },
  "Sync": {
    "FilStorageProvider": "InMemory"
  }
}
```

### SQL Server
If you have an existing SQL Server, you can use that and specify the connection string in the appsettings.json file.

If you would like to use a free version, you could download the latest version [SQL Server Express here](https://www.microsoft.com/en-au/sql-server/sql-server-downloads)

To use SQL Server with credentials, you can set like below:
```json
{
  "ConnectionStrings": {
    "Database": "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"
  },
  "Sync": {
    "FilStorageProvider": "SQLServer"
  }
}
```

To use SQL Server with integrated security (your current Windows identity), you can set like below:
```json
{
  "ConnectionStrings": {
    "Database": "Server=myServerAddress;Database=myDataBase;Integrated Security=SSPI;"
  },
  "Sync": {
    "FilStorageProvider": "SQLServer"
  }
}
```

### SQL Server Express LocalDB
If you want to use SQL Server Express LocalDB, which is free to use, you will need to bear in mind the database size limit is 10GB.

If the total number of files including the source, the target and the delta are large, for example, over 50M, then it's highly likely it will exceed the 10GB size limit.

To download the latest version (currently 2022), please click [SQL Server 2022 Express LocalDB](https://download.microsoft.com/download/3/8/d/38de7036-2433-4207-8eae-06e247e17b25/SqlLocalDB.msi).

To isntall, follow the steps of the installation wizard.

To use SQL Server Express LocalDB, you can set like below:
```json
{
  "ConnectionStrings": {
    "Database": "Server=(localdb)\v16.0;Integrated Security=true;"
  },
  "Sync": {
    "FilStorageProvider": "SQLServer"
  }
}
```

## ToDos
- Support NTFS USN Journal. Currently the logic is implemented, but the build/architecture of the project must be Windows x86, otherwise the relevant Win32 API will fail.
- Support multithreads for processing different folders parallelly. Currently it only support running multiple batch files concurrently.
