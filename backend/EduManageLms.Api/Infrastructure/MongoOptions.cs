namespace EduManageLms.Api.Infrastructure;
public sealed class MongoOptions { public const string SectionName="MongoDb"; public string ConnectionString{get;set;}="mongodb://localhost:27017"; public string DatabaseName{get;set;}="EduManageLms"; }
public sealed class JwtOptions { public const string SectionName="Jwt"; public string Issuer{get;set;}=""; public string Audience{get;set;}=""; public string Key{get;set;}=""; public int AccessTokenMinutes{get;set;}=20; public int RefreshTokenDays{get;set;}=7; }
public sealed class BackupOptions { public const string SectionName="Backup"; public string Directory{get;set;}="./backups"; public string MongoDumpPath{get;set;}="mongodump"; public string MongoRestorePath{get;set;}="mongorestore"; }
