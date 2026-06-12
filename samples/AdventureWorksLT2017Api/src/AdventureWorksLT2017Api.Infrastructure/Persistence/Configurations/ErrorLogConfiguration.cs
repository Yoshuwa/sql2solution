using AdventureWorksLT2017Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdventureWorksLT2017Api.Infrastructure.Persistence.Configurations;

public sealed class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("ErrorLog", "dbo");
        builder.HasKey(x => x.ErrorLogID);
        builder.Property(x => x.ErrorLogID)
            .HasColumnName("ErrorLogID")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.ErrorTime)
            .HasColumnName("ErrorTime");
        builder.Property(x => x.UserName)
            .HasColumnName("UserName")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.ErrorNumber)
            .HasColumnName("ErrorNumber");
        builder.Property(x => x.ErrorSeverity)
            .HasColumnName("ErrorSeverity");
        builder.Property(x => x.ErrorState)
            .HasColumnName("ErrorState");
        builder.Property(x => x.ErrorProcedure)
            .HasColumnName("ErrorProcedure")
            .HasMaxLength(126);
        builder.Property(x => x.ErrorLine)
            .HasColumnName("ErrorLine");
        builder.Property(x => x.ErrorMessage)
            .HasColumnName("ErrorMessage")
            .HasMaxLength(4000)
            .IsRequired();
    }
}
