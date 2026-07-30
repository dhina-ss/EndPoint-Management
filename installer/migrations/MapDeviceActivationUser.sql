CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718180126_InitialCreate') THEN
    CREATE TABLE devices (
        "Id" uuid NOT NULL,
        "DeviceId" character varying(100) NOT NULL,
        "DeviceName" character varying(200) NOT NULL,
        "SerialNumber" character varying(100) NOT NULL,
        "Manufacturer" character varying(100),
        "Model" character varying(100),
        "Processor" character varying(200),
        "RamSize" character varying(50),
        "StorageSize" character varying(50),
        "OSVersion" character varying(100),
        "OSBuildNumber" character varying(50),
        "IPAddress" character varying(45),
        "MACAddress" character varying(17),
        "Username" character varying(100),
        "LastBootTime" timestamp without time zone,
        "CreatedDate" timestamp with time zone NOT NULL,
        "UpdatedDate" timestamp with time zone NOT NULL,
        "LastSeen" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_devices" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718180126_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_devices_DeviceId" ON devices ("DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718180126_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718180126_InitialCreate', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718200729_AddDeviceAuthentication') THEN
    CREATE TABLE device_authentications (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "TokenHash" character varying(64) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        "LastUsedDate" timestamp with time zone,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_device_authentications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_device_authentications_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718200729_AddDeviceAuthentication') THEN
    CREATE UNIQUE INDEX "IX_device_authentications_DeviceId" ON device_authentications ("DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718200729_AddDeviceAuthentication') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718200729_AddDeviceAuthentication', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719051701_AddDeviceHeartbeat') THEN
    ALTER TABLE devices ADD "LastHeartbeatTime" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719051701_AddDeviceHeartbeat') THEN
    CREATE TABLE device_heartbeats (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "IPAddress" character varying(45),
        "Username" character varying(100),
        "AgentVersion" character varying(50),
        "HeartbeatTime" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_device_heartbeats" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_device_heartbeats_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719051701_AddDeviceHeartbeat') THEN
    CREATE INDEX "IX_device_heartbeats_DeviceId_HeartbeatTime" ON device_heartbeats ("DeviceId", "HeartbeatTime");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719051701_AddDeviceHeartbeat') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260719051701_AddDeviceHeartbeat', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721155808_AddAppUsageTracking') THEN
    CREATE TABLE app_usage_records (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "ApplicationName" character varying(200) NOT NULL,
        "UsageDate" date NOT NULL,
        "DurationSeconds" integer NOT NULL,
        "LastUpdated" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_app_usage_records" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_app_usage_records_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721155808_AddAppUsageTracking') THEN
    CREATE UNIQUE INDEX "IX_app_usage_records_DeviceId_ApplicationName_UsageDate" ON app_usage_records ("DeviceId", "ApplicationName", "UsageDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721155808_AddAppUsageTracking') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260721155808_AddAppUsageTracking', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722142320_AddUsbBlocking') THEN
    ALTER TABLE devices ADD "UsbBlockingEnabled" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722142320_AddUsbBlocking') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260722142320_AddUsbBlocking', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723064138_AddBlockedWebsites') THEN
    CREATE TABLE blocked_websites (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "Domain" character varying(253) NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_blocked_websites" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_blocked_websites_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723064138_AddBlockedWebsites') THEN
    CREATE UNIQUE INDEX "IX_blocked_websites_DeviceId_Domain" ON blocked_websites ("DeviceId", "Domain");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723064138_AddBlockedWebsites') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260723064138_AddBlockedWebsites', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "BatteryCharging" boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "BatteryPercent" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "CpuUsagePercent" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "DiskTotalGb" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "DiskUsagePercent" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "DiskUsedGb" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "HasBattery" boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "MemoryTotalMb" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "MemoryUsagePercent" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "MemoryUsedMb" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "NetworkReceivedKbps" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "NetworkSentKbps" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    ALTER TABLE device_heartbeats ADD "UptimeSeconds" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724115228_AddLiveMetrics') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260724115228_AddLiveMetrics', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724154903_AddApplicationInventory') THEN
    CREATE TABLE blocked_applications (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "ExecutableName" character varying(260) NOT NULL,
        "DisplayName" character varying(300),
        "CreatedDate" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_blocked_applications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_blocked_applications_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724154903_AddApplicationInventory') THEN
    CREATE TABLE installed_applications (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "Name" character varying(300) NOT NULL,
        "Version" character varying(100),
        "Publisher" character varying(200),
        "ExecutableName" character varying(260),
        "IsStoreApp" boolean NOT NULL,
        "ReportedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_installed_applications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_installed_applications_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724154903_AddApplicationInventory') THEN
    CREATE UNIQUE INDEX "IX_blocked_applications_DeviceId_ExecutableName" ON blocked_applications ("DeviceId", "ExecutableName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724154903_AddApplicationInventory') THEN
    CREATE INDEX "IX_installed_applications_DeviceId" ON installed_applications ("DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724154903_AddApplicationInventory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260724154903_AddApplicationInventory', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725091529_AddAppUsers') THEN
    CREATE TABLE app_users (
        "Id" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "EmployeeCode" character varying(50) NOT NULL,
        "Username" character varying(50) NOT NULL,
        "PasswordHash" text NOT NULL,
        "CreatedDate" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_app_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725091529_AddAppUsers') THEN
    CREATE UNIQUE INDEX "IX_app_users_Email" ON app_users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725091529_AddAppUsers') THEN
    CREATE UNIQUE INDEX "IX_app_users_EmployeeCode" ON app_users ("EmployeeCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725091529_AddAppUsers') THEN
    CREATE UNIQUE INDEX "IX_app_users_Username" ON app_users ("Username");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725091529_AddAppUsers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260725091529_AddAppUsers', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725162650_RemoveAppBlocking') THEN
    DROP TABLE blocked_applications;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725162650_RemoveAppBlocking') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260725162650_RemoveAppBlocking', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725173939_AddStoreGating') THEN
    ALTER TABLE devices ADD "StoreGatingEnabled" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260725173939_AddStoreGating') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260725173939_AddStoreGating', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727054550_AddSoftwareManagement') THEN
    CREATE TABLE installer_packages (
        "Id" uuid NOT NULL,
        "FileName" character varying(300) NOT NULL,
        "DisplayName" character varying(300) NOT NULL,
        "Kind" integer NOT NULL,
        "SilentArgs" character varying(500),
        "SizeBytes" bigint NOT NULL,
        "Sha256" character varying(64) NOT NULL,
        "Content" bytea NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_installer_packages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727054550_AddSoftwareManagement') THEN
    CREATE TABLE device_commands (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "Type" integer NOT NULL,
        "Status" integer NOT NULL,
        "TargetAppName" character varying(300),
        "TargetAppVersion" character varying(100),
        "TargetIsStoreApp" boolean NOT NULL,
        "PackageId" uuid,
        "ResultMessage" character varying(2000),
        "ResultCode" integer,
        "CreatedAt" timestamp with time zone NOT NULL,
        "DispatchedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        CONSTRAINT "PK_device_commands" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_device_commands_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_device_commands_installer_packages_PackageId" FOREIGN KEY ("PackageId") REFERENCES installer_packages ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727054550_AddSoftwareManagement') THEN
    CREATE INDEX "IX_device_commands_DeviceId_Status" ON device_commands ("DeviceId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727054550_AddSoftwareManagement') THEN
    CREATE INDEX "IX_device_commands_PackageId" ON device_commands ("PackageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727054550_AddSoftwareManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727054550_AddSoftwareManagement', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730073341_AddNetworkUsage') THEN
    CREATE TABLE network_usage_records (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "UsageDate" date NOT NULL,
        "BytesSent" bigint NOT NULL,
        "BytesReceived" bigint NOT NULL,
        "LastUpdated" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_network_usage_records" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_network_usage_records_devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES devices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730073341_AddNetworkUsage') THEN
    CREATE UNIQUE INDEX "IX_network_usage_records_DeviceId_UsageDate" ON network_usage_records ("DeviceId", "UsageDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730073341_AddNetworkUsage') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260730073341_AddNetworkUsage', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730092939_MapDeviceActivationUser') THEN
    ALTER TABLE devices ADD "ActivatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730092939_MapDeviceActivationUser') THEN
    ALTER TABLE devices ADD "ActivatedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730092939_MapDeviceActivationUser') THEN
    CREATE INDEX "IX_devices_ActivatedByUserId" ON devices ("ActivatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730092939_MapDeviceActivationUser') THEN
    ALTER TABLE devices ADD CONSTRAINT "FK_devices_app_users_ActivatedByUserId" FOREIGN KEY ("ActivatedByUserId") REFERENCES app_users ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730092939_MapDeviceActivationUser') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260730092939_MapDeviceActivationUser', '8.0.11');
    END IF;
END $EF$;
COMMIT;

