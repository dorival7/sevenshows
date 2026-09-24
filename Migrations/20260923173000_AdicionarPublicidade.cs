using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SevenShows.Api.Data;

#nullable disable
namespace SevenShows.Api.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20260923173000_AdicionarPublicidade")]
public partial class AdicionarPublicidade : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.CreateTable(name:"AdvertisingCampaigns",columns:table=>new {
   Id=table.Column<Guid>(type:"char(36)",nullable:false,collation:"ascii_general_ci"), AdvertiserName=table.Column<string>(type:"varchar(160)",maxLength:160,nullable:false), Title=table.Column<string>(type:"varchar(160)",maxLength:160,nullable:false), Description=table.Column<string>(type:"varchar(600)",maxLength:600,nullable:true), DesktopImageUrl=table.Column<string>(type:"varchar(255)",maxLength:255,nullable:true), MobileImageUrl=table.Column<string>(type:"varchar(255)",maxLength:255,nullable:true), Position=table.Column<string>(type:"varchar(40)",maxLength:40,nullable:false), DestinationType=table.Column<string>(type:"varchar(30)",maxLength:30,nullable:false), DestinationUrl=table.Column<string>(type:"varchar(500)",maxLength:500,nullable:true), WhatsApp=table.Column<string>(type:"varchar(30)",maxLength:30,nullable:true), WhatsAppMessage=table.Column<string>(type:"varchar(500)",maxLength:500,nullable:true), InstagramUrl=table.Column<string>(type:"varchar(500)",maxLength:500,nullable:true), WebsiteUrl=table.Column<string>(type:"varchar(500)",maxLength:500,nullable:true), Slug=table.Column<string>(type:"varchar(180)",maxLength:180,nullable:true), City=table.Column<string>(type:"varchar(180)",maxLength:180,nullable:true), State=table.Column<string>(type:"varchar(2)",maxLength:2,nullable:true), LogoUrl=table.Column<string>(type:"varchar(255)",maxLength:255,nullable:true), StartDate=table.Column<DateTime>(type:"datetime(6)",nullable:false), EndDate=table.Column<DateTime>(type:"datetime(6)",nullable:false), Status=table.Column<string>(type:"varchar(20)",maxLength:20,nullable:false), ContractValue=table.Column<decimal>(type:"decimal(18,2)",nullable:false), Impressions=table.Column<long>(type:"bigint",nullable:false), Clicks=table.Column<long>(type:"bigint",nullable:false), WhatsAppClicks=table.Column<long>(type:"bigint",nullable:false), InstagramClicks=table.Column<long>(type:"bigint",nullable:false), WebsiteClicks=table.Column<long>(type:"bigint",nullable:false), CreatedAt=table.Column<DateTime>(type:"datetime(6)",nullable:false), UpdatedAt=table.Column<DateTime>(type:"datetime(6)",nullable:false)
  },constraints:table=>table.PrimaryKey("PK_AdvertisingCampaigns",x=>x.Id));
  migrationBuilder.CreateIndex(name:"IX_AdvertisingCampaigns_Slug",table:"AdvertisingCampaigns",column:"Slug");
  migrationBuilder.CreateIndex(name:"IX_AdvertisingCampaigns_Status_StartDate_EndDate",table:"AdvertisingCampaigns",columns:new[]{"Status","StartDate","EndDate"});
 }
 protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.DropTable(name:"AdvertisingCampaigns");
}
