# UBookIt.Persistence

EF Core persistence for uBookIt.

## Requirements

- **SQL Server 2019 or later** (including LocalDB and Azure SQL). uBookIt does
  not support SQLite or any other database provider; a site configured with a
  different provider fails at startup with a clear error.
- The package uses the site's existing Umbraco connection string
  (`umbracoDbDSN`). No additional connection configuration is needed.

## Behaviour

- Database migrations apply automatically at application startup and are
  recorded in the package-private `__uBookItEFMigrationsHistory` table. All
  uBookIt tables carry the `uBookIt` prefix. Migrations are additive-only.
- In a load-balanced setup, run the first boot after installing or upgrading
  the package on a single instance.

## Configuration

```json
{
  "UBookIt": {
    "TimeZoneId": "Europe/London"
  }
}
```

`UBookIt:TimeZoneId` is the site-wide IANA booking time zone. When absent it
defaults to `UTC` and a warning is logged at startup.
