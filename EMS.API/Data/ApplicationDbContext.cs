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
    }
}
