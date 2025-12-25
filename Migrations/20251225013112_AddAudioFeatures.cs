using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SLSKDONET.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Danceability",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Energy",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ISRC",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ManualBPM",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualKey",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SpotifyBPM",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpotifyKey",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Valence",
                table: "Tracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bitrate",
                table: "PlaylistTracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ISRC",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ManualBPM",
                table: "PlaylistTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualKey",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinBitrateOverride",
                table: "PlaylistTracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredFormats",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SpotifyBPM",
                table: "PlaylistTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpotifyKey",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Danceability",
                table: "LibraryEntries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Energy",
                table: "LibraryEntries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ISRC",
                table: "LibraryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnriched",
                table: "LibraryEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "ManualBPM",
                table: "LibraryEntries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualKey",
                table: "LibraryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SpotifyBPM",
                table: "LibraryEntries",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpotifyKey",
                table: "LibraryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Valence",
                table: "LibraryEntries",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LibraryHealth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TotalTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    HqTracks = table.Column<int>(type: "INTEGER", nullable: false),
                    UpgradableCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PendingUpdates = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalStorageBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    FreeStorageBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    LastScanDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TopGenresJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryHealth", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryHealth");

            migrationBuilder.DropColumn(
                name: "Danceability",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Energy",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "ISRC",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "ManualBPM",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "ManualKey",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "SpotifyBPM",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "SpotifyKey",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Valence",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Bitrate",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "ISRC",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "ManualBPM",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "ManualKey",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "MinBitrateOverride",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "PreferredFormats",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "SpotifyBPM",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "SpotifyKey",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "Danceability",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "Energy",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "ISRC",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "IsEnriched",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "ManualBPM",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "ManualKey",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "SpotifyBPM",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "SpotifyKey",
                table: "LibraryEntries");

            migrationBuilder.DropColumn(
                name: "Valence",
                table: "LibraryEntries");
        }
    }
}
