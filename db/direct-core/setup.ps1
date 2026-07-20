param(
    [switch]$Check,
    [switch]$Drop,
    [switch]$CleanMigrate,
    [switch]$MigrateOnly
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$baseline = Join-Path $scriptDir "ffxiv_server.sql"
$migrations = Join-Path $scriptDir "migrations"
$backupDir = if ($env:AETHERXIV_CORE_BACKUP_DIR) { $env:AETHERXIV_CORE_BACKUP_DIR } else { Join-Path $env:LOCALAPPDATA "AetherXIV\backups\database" }
$dbHost = if ($env:AETHERXIV_DB_HOST) { $env:AETHERXIV_DB_HOST } else { "127.0.0.1" }
$dbPort = if ($env:AETHERXIV_DB_PORT) { $env:AETHERXIV_DB_PORT } else { "3306" }
$dbName = if ($env:AETHERXIV_DB_NAME) { $env:AETHERXIV_DB_NAME } else { "ffxiv_server" }
$appUser = if ($env:AETHERXIV_DB_USER) { $env:AETHERXIV_DB_USER } else { "aetherxiv" }
$appPass = if ($env:AETHERXIV_DB_PASSWORD) { $env:AETHERXIV_DB_PASSWORD } else { "aether_dev" }
$adminUser = if ($env:AETHERXIV_DB_ADMIN_USER) { $env:AETHERXIV_DB_ADMIN_USER } else { "root" }
$adminPass = if ($env:AETHERXIV_DB_ADMIN_PASSWORD) { $env:AETHERXIV_DB_ADMIN_PASSWORD } else { "" }
$allowedHosts = if ($env:AETHERXIV_DB_ALLOWED_HOSTS) { $env:AETHERXIV_DB_ALLOWED_HOSTS } else { "localhost,127.0.0.1" }

if ($MigrateOnly -and ($Drop -or $CleanMigrate)) {
    throw "-MigrateOnly cannot be combined with -Drop or -CleanMigrate."
}

function Resolve-DatabaseTool([string[]]$Names) {
    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }

    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramW6432) | Where-Object { $_ } | Select-Object -Unique
    foreach ($root in $roots) {
        foreach ($name in $Names) {
            $patterns = @(
                (Join-Path $root "MariaDB*\bin\$name.exe"),
                (Join-Path $root "MySQL\MySQL Server *\bin\$name.exe")
            )
            foreach ($pattern in $patterns) {
                $match = Get-ChildItem $pattern -File -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
                if ($match) { return $match.FullName }
            }
        }
    }
    throw "Could not locate any of: $($Names -join ', ')."
}

$mysql = Resolve-DatabaseTool @("mariadb", "mysql")
$dump = try { Resolve-DatabaseTool @("mariadb-dump", "mysqldump") } catch { $null }

function Sql-Literal([string]$Value) { return $Value.Replace("\", "\\").Replace("'", "''") }
function Sql-Identifier([string]$Value) { return $Value.Replace('`', '``') }
function Connection-Args([string]$User, [string]$Password) {
    $result = @("-h", $dbHost, "-P", $dbPort, "-u", $User)
    if ($Password) { $result += "-p$Password" }
    return $result
}

$adminArgs = Connection-Args $adminUser $adminPass
$appArgs = Connection-Args $appUser $appPass
if ($MigrateOnly) { $adminArgs = $appArgs }

function Invoke-Query([string[]]$Arguments, [string]$Sql, [string]$Database = "") {
    $all = @($Arguments)
    if ($Database) { $all += $Database }
    $all += @("-N", "-B", "-e", $Sql)
    $output = & $mysql @all
    if ($LASTEXITCODE -ne 0) { throw "Database command failed." }
    return ($output | Out-String).Trim()
}

function Invoke-SqlFile([string]$Path, [string]$Database) {
    $info = [Diagnostics.ProcessStartInfo]::new($mysql)
    foreach ($argument in $adminArgs) { [void]$info.ArgumentList.Add($argument) }
    [void]$info.ArgumentList.Add($Database)
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $process = [Diagnostics.Process]::Start($info)
    $source = [IO.File]::OpenRead($Path)
    try { $source.CopyTo($process.StandardInput.BaseStream) }
    finally { $source.Dispose(); $process.StandardInput.Close() }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "SQL import failed: $Path" }
}

function Backup-Database {
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    $basePath = Join-Path $backupDir "$dbName-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))"
    $path = "$basePath.sql"
    $suffix = 0
    while ((Test-Path $path) -or (Test-Path "$path.sha256")) {
        $suffix++
        $path = "$basePath-$suffix.sql"
    }
    $tableCount = Invoke-Query $adminArgs "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$dbLiteral'"
    if ($tableCount -eq "0") {
        Set-Content -Encoding utf8 $path "-- AetherXIV backup: $dbName existed and contained no tables."
    }
    else {
        if (-not $dump) { throw "MariaDB/MySQL dump client is required to preserve the existing non-empty database before rebuilding it." }
        $info = [Diagnostics.ProcessStartInfo]::new($dump)
        foreach ($argument in $adminArgs) { [void]$info.ArgumentList.Add($argument) }
        foreach ($argument in @("--routines", "--triggers", "--single-transaction", $dbName)) { [void]$info.ArgumentList.Add($argument) }
        $info.UseShellExecute = $false
        $info.RedirectStandardOutput = $true
        $process = [Diagnostics.Process]::Start($info)
        $destination = [IO.File]::Create($path)
        try { $process.StandardOutput.BaseStream.CopyTo($destination) }
        finally { $destination.Dispose() }
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Database backup failed." }
    }
    $hash = (Get-FileHash -Algorithm SHA256 $path).Hash.ToLowerInvariant()
    Set-Content -Encoding ascii "$path.sha256" "$hash  $([IO.Path]::GetFileName($path))"
    $script:LastBackupPath = $path
    if (-not $script:OriginalBackupPath) { $script:OriginalBackupPath = $path }
    Write-Host "Backed up $dbName to $path"
}

function Restore-OriginalDatabase {
    if ($script:OriginalBackupPath -and (Test-Path $script:OriginalBackupPath -PathType Leaf)) {
        Write-Warning "Restoring the untouched database backup: $script:OriginalBackupPath"
        [void](Invoke-Query $adminArgs "DROP DATABASE IF EXISTS ``$dbId``; CREATE DATABASE ``$dbId`` CHARACTER SET utf8 COLLATE utf8_general_ci")
        Invoke-SqlFile $script:OriginalBackupPath $dbName
    }
    else {
        Write-Warning "No original database existed; removing the incomplete new database so setup can be retried."
        [void](Invoke-Query $adminArgs "DROP DATABASE IF EXISTS ``$dbId``")
    }
}

function Export-PlayerData([string]$Path, [string[]]$Tables) {
    $info = [Diagnostics.ProcessStartInfo]::new($dump)
    foreach ($argument in $adminArgs) { [void]$info.ArgumentList.Add($argument) }
    foreach ($argument in @("--single-transaction", "--no-create-info", "--complete-insert", "--replace", "--hex-blob", "--skip-triggers", $dbName)) {
        [void]$info.ArgumentList.Add($argument)
    }
    foreach ($table in $Tables) { [void]$info.ArgumentList.Add($table) }
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $process = [Diagnostics.Process]::Start($info)
    $destination = [IO.File]::Create($Path)
    try { $process.StandardOutput.BaseStream.CopyTo($destination) }
    finally { $destination.Dispose() }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Player-data backup failed." }
    $hash = (Get-FileHash -Algorithm SHA256 $Path).Hash.ToLowerInvariant()
    Set-Content -Encoding ascii "$Path.sha256" "$hash  $([IO.Path]::GetFileName($Path))"
}

function Test-Database {
    $schema = Sql-Literal $dbName
    $required = @("users", "sessions", "servers", "characters", "characters_appearance",
        "characters_quest_scenario", "characters_quest_completed", "characters_hotbar", "server_sessions",
        "server_zones", "server_zones_privateareas", "server_battlenpc_spawn_locations",
        "server_battle_commands", "server_player_base_stats", "characters_class_attributes", "server_battlenpc_spawn_audit_pins",
        "server_spawn_locations", "gamedata_actor_class", "gamedata_actor_appearance", "server_items_modifiers",
        "characters_inventory", "characters_chocobo", "server_npc_spawn_evidence", "server_npc_spawn_evidence_catalog",
        "aether_database_compatibility",
        "launcher_config", "launcher_status", "launcher_news", "launcher_patch_files",
        "launcher_presentation", "launcher_reel_text")
    $missing = @()
    foreach ($table in $required) {
        $count = Invoke-Query $appArgs "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$schema' AND table_name='$table'"
        if ($count -ne "1") { $missing += $table }
    }
    if ($missing.Count) { throw "Database schema is incomplete: $($missing -join ', ')" }
    $zones = Invoke-Query $appArgs "SELECT COUNT(*) FROM server_zones" $dbName
    $commands = Invoke-Query $appArgs "SELECT COUNT(*) FROM server_battle_commands" $dbName
    $stats = Invoke-Query $appArgs "SELECT COUNT(*) FROM server_player_base_stats" $dbName
    $launcherColumns = Invoke-Query $appArgs "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='launcher_config' AND column_name IN ('service_version','server_name','is_active')" $dbName
    if ([int]$zones -eq 0 -or [int]$commands -eq 0 -or [int]$stats -eq 0 -or $launcherColumns -ne "3") {
        throw "Database seed/launcher verification failed."
    }
    $npcServiceContract = Invoke-Query $appArgs "SELECT CONCAT(COUNT(*),':',COALESCE(MAX(version),''),':',COALESCE(MAX(contentHashSha256),''),':',COALESCE(MAX(recordCount),0)) FROM server_npc_spawn_evidence_catalog WHERE catalogId='zone-service-npcs-1.23b'" $dbName
    if ($npcServiceContract -ne "1:2026.07.19.1:f40276dea0ce6739b40d0dca3dc44f665ee525646851592a9439d5013f97b8de:23") {
        throw "NPC service seed contract mismatch: $npcServiceContract"
    }
    $centralShroudPinspawnContract = Invoke-Query $appArgs "SELECT CONCAT((SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins WHERE zoneId=150 AND createdByCharacterName='Akhebica Loha' AND promotionNote LIKE 'Source dump pin #%'),':',(SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins WHERE zoneId=150 AND createdByCharacterName='Akhebica Loha' AND isPromoted=1 AND enemyName='Star Marmot' AND promotionMigration='20260718_000013_central_shroud_pinspawn_restore'),':',(SELECT COUNT(*) FROM server_battlenpc_spawn_locations s JOIN server_battlenpc_groups g ON g.groupId=s.groupId JOIN server_battlenpc_pools p ON p.poolId=g.poolId WHERE s.bnpcId IN (1500001,1500002,1500034,1500035,1500039,1500041,1500042,1500051,1500055,1500056,1500057,1500058,1500060) AND g.zoneId=150 AND g.minLevel=3 AND g.maxLevel=4 AND g.hp=99 AND g.mp=130 AND p.actorClassId IN (2104009,2104028) AND p.genusId=12),':',(SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins WHERE zoneId=150 AND promotionNote LIKE 'Source dump pin #%' AND isPromoted=1 AND enemyName<>'Star Marmot'))" $dbName
    if ($centralShroudPinspawnContract -ne "60:11:13:0") {
        throw "Central Shroud pinspawn contract mismatch: $centralShroudPinspawnContract"
    }
    $contract = Invoke-Query $appArgs "SELECT CONCAT(schema_generation,':',schema_version,':',compatibility_id,':',baseline_id) FROM aether_database_compatibility WHERE compatibility_key='direct-core' LIMIT 1" $dbName
    if ($contract -ne "2:1:aetherxiv-direct-core-v2:20260716_000001_ffxiv_server_v2_baseline") {
        throw "Database compatibility mismatch: $contract"
    }
    Write-Host "Direct-core database verified: $dbName (zones=$zones commands=$commands baseStats=$stats)"
}

function Test-CurrentV2Contract {
    $compatibilityTable = Invoke-Query $adminArgs "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$dbLiteral' AND table_name='aether_database_compatibility'"
    if ($compatibilityTable -ne "1") { return $false }
    $contract = Invoke-Query $adminArgs "SELECT COUNT(*) FROM aether_database_compatibility WHERE compatibility_key='direct-core' AND schema_generation=2 AND schema_version=1 AND compatibility_id='aetherxiv-direct-core-v2' AND baseline_id='20260716_000001_ffxiv_server_v2_baseline'" $dbName
    return $contract -eq "1"
}

if ($Check) { Test-Database; exit 0 }
if (-not (Test-Path $baseline -PathType Leaf) -or -not (Test-Path $migrations -PathType Container)) {
    throw "Database package is incomplete."
}

$dbId = Sql-Identifier $dbName
$dbLiteral = Sql-Literal $dbName
$userLiteral = Sql-Literal $appUser
$passLiteral = Sql-Literal $appPass
$exists = Invoke-Query $adminArgs "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name='$dbLiteral'"
$script:LastBackupPath = ""
$script:OriginalBackupPath = if ($env:AETHERXIV_ORIGINAL_BACKUP_PATH) { $env:AETHERXIV_ORIGINAL_BACKUP_PATH } else { "" }
$baselineImported = $false

if (-not $MigrateOnly -and -not $Drop -and -not $CleanMigrate -and $exists -eq "1" -and -not (Test-CurrentV2Contract)) {
    Write-Host "Existing database is empty or predates AetherXIV 2; preserving a full backup before installing the canonical database."
    $CleanMigrate = $true
}

if ($MigrateOnly) {
    if ($exists -ne "1") { throw "The configured database does not exist; administrative setup is required." }
    if (-not (Test-CurrentV2Contract)) { throw "The configured database is not an AetherXIV 2 database; administrator-assisted setup is required." }
    Backup-Database
    Write-Host "Applying pending migrations with configured account $appUser."
}
else {
    if ($CleanMigrate) {
        if ($exists -ne "1") { throw "Clean migration requires an existing database." }
        $usersTable = Invoke-Query $adminArgs "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$dbLiteral' AND table_name='users'"
        $charactersTable = Invoke-Query $adminArgs "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$dbLiteral' AND table_name='characters'"
        Backup-Database
        $canRestorePlayers = $usersTable -eq "1" -and $charactersTable -eq "1"
        $playerData = ""
        $usersBefore = "0"
        $charactersBefore = "0"
        if ($canRestorePlayers) {
            $playerData = $script:LastBackupPath.Substring(0, $script:LastBackupPath.Length - 4) + "-player-data.sql"
            $usersBefore = Invoke-Query $adminArgs "SELECT COUNT(*) FROM users" $dbName
            $charactersBefore = Invoke-Query $adminArgs "SELECT COUNT(*) FROM characters" $dbName
            $candidates = [Collections.Generic.List[string]]::new()
            foreach ($table in @("users", "characters")) { $candidates.Add($table) }
            $characterTables = Invoke-Query $adminArgs "SELECT table_name FROM information_schema.tables WHERE table_schema='$dbLiteral' AND table_name LIKE 'characters\\_%' ORDER BY table_name"
            foreach ($table in ($characterTables -split "`r?`n" | Where-Object { $_ })) { $candidates.Add($table) }
            $preserved = [Collections.Generic.List[string]]::new()
            foreach ($table in $candidates) {
                if ($preserved.Contains($table)) { continue }
                $tableLiteral = Sql-Literal $table
                if ((Invoke-Query $adminArgs "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$dbLiteral' AND table_name='$tableLiteral'") -eq "1") {
                    $preserved.Add($table)
                }
            }
            Export-PlayerData $playerData $preserved.ToArray()
        }
        else {
            Write-Host "No complete users/characters pair was found; installing a fresh canonical database. The full backup is retained for manual recovery."
        }

        try {
            [void](Invoke-Query $adminArgs "DROP DATABASE ``$dbId``; CREATE DATABASE ``$dbId`` CHARACTER SET utf8 COLLATE utf8_general_ci")
            Invoke-SqlFile $baseline $dbName
        }
        catch {
            Write-Warning "Canonical database installation failed; restoring the untouched full backup."
            Restore-OriginalDatabase
            throw
        }

        if ($canRestorePlayers) {
            try {
                Invoke-SqlFile $playerData $dbName
                $usersAfter = Invoke-Query $adminArgs "SELECT COUNT(*) FROM users" $dbName
                $charactersAfter = Invoke-Query $adminArgs "SELECT COUNT(*) FROM characters" $dbName
                if ($usersBefore -ne $usersAfter -or $charactersBefore -ne $charactersAfter) {
                    throw "Player migration count mismatch."
                }
                Write-Host "Migrated $usersAfter accounts and $charactersAfter characters into the AetherXIV 2 baseline. Player-data copy: $playerData"
            }
            catch {
                Write-Warning "Player data was incompatible with the canonical schema; keeping the fresh database. The full backup and player-data copy are retained."
                try {
                    [void](Invoke-Query $adminArgs "DROP DATABASE IF EXISTS ``$dbId``; CREATE DATABASE ``$dbId`` CHARACTER SET utf8 COLLATE utf8_general_ci")
                    Invoke-SqlFile $baseline $dbName
                }
                catch {
                    Write-Warning "Fresh database recovery failed; restoring the untouched full backup."
                    Restore-OriginalDatabase
                    throw
                }
            }
        }
        $baselineImported = $true
        Write-Host "Canonical AetherXIV 2 database installed. Full backup: $script:LastBackupPath"
    }

    if ($exists -eq "1" -and $Drop) {
        Backup-Database
        [void](Invoke-Query $adminArgs "DROP DATABASE ``$dbId``")
        $exists = "0"
    }

    [void](Invoke-Query $adminArgs "CREATE DATABASE IF NOT EXISTS ``$dbId`` CHARACTER SET utf8 COLLATE utf8_general_ci")
    $applicationHosts = @($allowedHosts -split "," | ForEach-Object { $_.Trim() })
    if ($applicationHosts.Count -eq 0 -or @($applicationHosts | Where-Object { -not $_ }).Count -gt 0) {
        throw "Database application hosts cannot be empty."
    }
    foreach ($hostName in $applicationHosts) {
        $hostLiteral = Sql-Literal $hostName
        [void](Invoke-Query $adminArgs "CREATE USER IF NOT EXISTS '$userLiteral'@'$hostLiteral' IDENTIFIED BY '$passLiteral'; ALTER USER '$userLiteral'@'$hostLiteral' IDENTIFIED BY '$passLiteral'; GRANT ALL PRIVILEGES ON ``$dbId``.* TO '$userLiteral'@'$hostLiteral'")
    }
    [void](Invoke-Query $adminArgs "FLUSH PRIVILEGES")

    if ($baselineImported) {
        Write-Host "Canonical baseline is installed in $dbName"
    } elseif ($exists -eq "0") {
        Write-Host "Importing canonical direct-core baseline into $dbName"
        try { Invoke-SqlFile $baseline $dbName }
        catch {
            Write-Warning "Canonical database installation failed."
            Restore-OriginalDatabase
            throw
        }
        $baselineImported = $true
    } else {
        Write-Host "Existing AetherXIV 2 database detected; checking its migration ledger and canonical schema."
        Backup-Database
    }
}

try {
    [void](Invoke-Query $adminArgs "CREATE TABLE IF NOT EXISTS aether_schema_migrations (migration_name varchar(255) NOT NULL, checksum_sha256 char(64) NOT NULL, applied_at timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (migration_name)) ENGINE=InnoDB DEFAULT CHARSET=utf8" $dbName)
    $baselineHash = (Get-FileHash -Algorithm SHA256 $baseline).Hash.ToLowerInvariant()
    $recordedBaseline = Invoke-Query $adminArgs "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='baseline/20260716_000001_ffxiv_server_v2' LIMIT 1" $dbName
    if ($MigrateOnly -and -not $recordedBaseline) {
        throw "The existing database has no recorded AetherXIV 2 baseline; administrator-assisted repair is required."
    }
    if (-not $MigrateOnly -and $recordedBaseline -and $recordedBaseline -ne $baselineHash) { throw "Baseline checksum mismatch." }
    if (-not $recordedBaseline) {
        [void](Invoke-Query $adminArgs "INSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('baseline/20260716_000001_ffxiv_server_v2','$baselineHash')" $dbName)
    }

    foreach ($migration in Get-ChildItem -Path $migrations -Filter *.sql | Sort-Object Name) {
        $hash = (Get-FileHash -Algorithm SHA256 $migration.FullName).Hash.ToLowerInvariant()
        $recorded = Invoke-Query $adminArgs "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='$($migration.Name)' LIMIT 1" $dbName
        if ($recorded) {
            if ($recorded -ne $hash) {
                if ($MigrateOnly) {
                    Write-Warning "Already-applied migration file differs from its recorded checksum and will not be reapplied: $($migration.Name)"
                }
                else {
                    throw "Migration checksum mismatch: $($migration.Name)"
                }
            }
            continue
        }
        Write-Host "Applying $($migration.Name)"
        Invoke-SqlFile $migration.FullName $dbName
        [void](Invoke-Query $adminArgs "INSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('$($migration.Name)','$hash')" $dbName)
    }

    Test-Database
}
catch {
    if (-not $MigrateOnly -and -not $baselineImported) {
        Write-Warning "The existing AetherXIV 2 schema is incomplete or stale; preserving it and rebuilding the canonical schema."
        $previousOriginalBackup = $env:AETHERXIV_ORIGINAL_BACKUP_PATH
        $env:AETHERXIV_ORIGINAL_BACKUP_PATH = $script:OriginalBackupPath
        try { & $PSCommandPath -CleanMigrate }
        finally {
            if ($null -eq $previousOriginalBackup) { Remove-Item Env:AETHERXIV_ORIGINAL_BACKUP_PATH -ErrorAction SilentlyContinue }
            else { $env:AETHERXIV_ORIGINAL_BACKUP_PATH = $previousOriginalBackup }
        }
        exit 0
    }
    if ($baselineImported) { Restore-OriginalDatabase }
    throw
}
