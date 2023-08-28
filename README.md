# Locking

Testing optimistic and pessimistic locking.

## Run local

### Sql Server

`
docker run --name sqlserver -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=yourStrong(!)Password" -p 1433:1433 -d mcr.microsoft.com/mssql/server
`