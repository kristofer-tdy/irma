using System;
using Flir.Irma.WebApi.Data;
using Flir.Irma.WebApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Flir.Irma.WebApi.Migrations
{
    [DbContext(typeof(IrmaDbContext))]
    partial class IrmaDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0");

            modelBuilder.Entity("Flir.Irma.WebApi.Entities.ConversationEntity", b =>
                {
                    b.Property<Guid>("ConversationId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedDateTimeUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<DateTime>("LastModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("Product")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<int>("State")
                        .HasColumnType("int");

                    b.Property<int>("TurnCount")
                        .HasColumnType("int");

                    b.HasKey("ConversationId");

                    b.HasIndex("CreatedDateTimeUtc");

                    b.ToTable("Conversations");
                });

            modelBuilder.Entity("Flir.Irma.WebApi.Entities.MessageEntity", b =>
                {
                    b.Property<Guid>("MessageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ConversationId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedDateTimeUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Role")
                        .HasColumnType("int");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.HasKey("MessageId");

                    b.HasIndex("ConversationId", "CreatedDateTimeUtc");

                    b.ToTable("Messages");

                    b.HasOne("Flir.Irma.WebApi.Entities.ConversationEntity", "Conversation")
                        .WithMany("Messages")
                        .HasForeignKey("ConversationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Conversation");
                });

            modelBuilder.Entity("Flir.Irma.WebApi.Entities.ConversationEntity", b =>
                {
                    b.Navigation("Messages");
                });
#pragma warning restore 612, 618
        }
    }
}

