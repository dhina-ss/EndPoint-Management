using EMS.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMS.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DeviceAuthentication> DeviceAuthentications => Set<DeviceAuthentication>();

    public DbSet<DeviceHeartbeat> DeviceHeartbeats => Set<DeviceHeartbeat>();

    public DbSet<AppUsageRecord> AppUsageRecords => Set<AppUsageRecord>();

    public DbSet<BlockedWebsite> BlockedWebsites => Set<BlockedWebsite>();

    public DbSet<InstalledApplication> InstalledApplications => Set<InstalledApplication>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<InstallerPackage> InstallerPackages => Set<InstallerPackage>();

    public DbSet<DeviceCommand> DeviceCommands => Set<DeviceCommand>();

    public DbSet<NetworkUsageRecord> NetworkUsageRecords => Set<NetworkUsageRecord>();

    public DbSet<WorkSessionRecord> WorkSessionRecords => Set<WorkSessionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");

            entity.HasKey(d => d.Id);

            entity.HasIndex(d => d.DeviceId)
                .IsUnique();

            entity.Property(d => d.DeviceId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(d => d.DeviceName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(d => d.SerialNumber)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(d => d.Manufacturer).HasMaxLength(100);
            entity.Property(d => d.Model).HasMaxLength(100);
            entity.Property(d => d.Processor).HasMaxLength(200);
            entity.Property(d => d.RamSize).HasMaxLength(50);
            entity.Property(d => d.StorageSize).HasMaxLength(50);
            entity.Property(d => d.OSVersion).HasMaxLength(100);
            entity.Property(d => d.OSBuildNumber).HasMaxLength(50);
            entity.Property(d => d.IPAddress).HasMaxLength(45);
            entity.Property(d => d.MACAddress).HasMaxLength(17);
            entity.Property(d => d.Username).HasMaxLength(100);

            // Boot time is reported as the endpoint's local wall-clock time,
            // so it must not be normalized to UTC by the timestamptz mapping.
            entity.Property(d => d.LastBootTime)
                .HasColumnType("timestamp without time zone");

            entity.Property(d => d.UsbBlockingEnabled)
                .HasDefaultValue(false);

            entity.Property(d => d.StoreGatingEnabled)
                .HasDefaultValue(false);

            // The user who activated this device. Keep the device if that user
            // is later removed - just clear the link.
            entity.HasOne(d => d.ActivatedByUser)
                .WithMany()
                .HasForeignKey(d => d.ActivatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeviceAuthentication>(entity =>
        {
            entity.ToTable("device_authentications");

            entity.HasKey(a => a.Id);

            // One credential per device.
            entity.HasIndex(a => a.DeviceId)
                .IsUnique();

            entity.Property(a => a.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.HasOne(a => a.Device)
                .WithOne(d => d.Authentication)
                .HasForeignKey<DeviceAuthentication>(a => a.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceHeartbeat>(entity =>
        {
            entity.ToTable("device_heartbeats");

            entity.HasKey(h => h.Id);

            // Serves "latest heartbeats for device X" queries.
            entity.HasIndex(h => new { h.DeviceId, h.HeartbeatTime });

            entity.Property(h => h.IPAddress).HasMaxLength(45);
            entity.Property(h => h.Username).HasMaxLength(100);
            entity.Property(h => h.AgentVersion).HasMaxLength(50);

            entity.HasOne(h => h.Device)
                .WithMany()
                .HasForeignKey(h => h.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUsageRecord>(entity =>
        {
            entity.ToTable("app_usage_records");

            entity.HasKey(a => a.Id);

            // One row per device/app/day; the service upserts into this
            // instead of inserting a new row on every report.
            entity.HasIndex(a => new { a.DeviceId, a.ApplicationName, a.UsageDate })
                .IsUnique();

            entity.Property(a => a.ApplicationName)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasOne(a => a.Device)
                .WithMany()
                .HasForeignKey(a => a.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BlockedWebsite>(entity =>
        {
            entity.ToTable("blocked_websites");

            entity.HasKey(b => b.Id);

            // A domain can appear at most once per device.
            entity.HasIndex(b => new { b.DeviceId, b.Domain })
                .IsUnique();

            entity.Property(b => b.Domain)
                .HasMaxLength(253)
                .IsRequired();

            entity.HasOne(b => b.Device)
                .WithMany()
                .HasForeignKey(b => b.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InstalledApplication>(entity =>
        {
            entity.ToTable("installed_applications");

            entity.HasKey(a => a.Id);

            entity.HasIndex(a => a.DeviceId);

            entity.Property(a => a.Name).HasMaxLength(300).IsRequired();
            entity.Property(a => a.Version).HasMaxLength(100);
            entity.Property(a => a.Publisher).HasMaxLength(200);
            entity.Property(a => a.ExecutableName).HasMaxLength(260);

            entity.HasOne(a => a.Device)
                .WithMany()
                .HasForeignKey(a => a.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkSessionRecord>(entity =>
        {
            entity.ToTable("work_session_records");

            entity.HasKey(w => w.Id);

            // One row per device/day; the service upserts into this.
            entity.HasIndex(w => new { w.DeviceId, w.WorkDate }).IsUnique();

            entity.HasOne(w => w.Device)
                .WithMany()
                .HasForeignKey(w => w.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NetworkUsageRecord>(entity =>
        {
            entity.ToTable("network_usage_records");

            entity.HasKey(n => n.Id);

            // One row per device/day; the service upserts into this.
            entity.HasIndex(n => new { n.DeviceId, n.UsageDate }).IsUnique();

            entity.HasOne(n => n.Device)
                .WithMany()
                .HasForeignKey(n => n.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InstallerPackage>(entity =>
        {
            entity.ToTable("installer_packages");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.FileName).HasMaxLength(300).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(300).IsRequired();
            entity.Property(p => p.SilentArgs).HasMaxLength(500);
            entity.Property(p => p.Sha256).HasMaxLength(64).IsRequired();

            // Raw installer bytes; Npgsql maps byte[] to bytea by default.
            entity.Property(p => p.Content).IsRequired();
        });

        modelBuilder.Entity<DeviceCommand>(entity =>
        {
            entity.ToTable("device_commands");

            entity.HasKey(c => c.Id);

            // Serves the agent's "pending commands for this device" poll.
            entity.HasIndex(c => new { c.DeviceId, c.Status });

            entity.Property(c => c.TargetAppName).HasMaxLength(300);
            entity.Property(c => c.TargetAppVersion).HasMaxLength(100);
            entity.Property(c => c.ResultMessage).HasMaxLength(2000);

            entity.HasOne(c => c.Device)
                .WithMany()
                .HasForeignKey(c => c.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Keep a package as long as commands reference it; deleting a
            // package is blocked while in-flight commands still point at it.
            entity.HasOne(c => c.Package)
                .WithMany()
                .HasForeignKey(c => c.PackageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("app_users");

            entity.HasKey(u => u.Id);

            // Each of these identifies a user, so each is unique.
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.EmployeeCode).IsUnique();

            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.EmployeeCode).HasMaxLength(50).IsRequired();
            entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
        });
    }
}
