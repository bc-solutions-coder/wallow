-- init-keycloak-db.sql
-- Creates a separate database and dedicated user for Keycloak within the same Postgres instance.
-- This script runs on first initialization only (when the postgres_data volume is empty).

SELECT 'CREATE DATABASE keycloak_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak_db')\gexec

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'keycloak_user') THEN
        CREATE ROLE keycloak_user WITH LOGIN PASSWORD 'FoundryKeycloak123!';
    END IF;
END
$$;

GRANT ALL PRIVILEGES ON DATABASE keycloak_db TO keycloak_user;

\c keycloak_db
GRANT ALL ON SCHEMA public TO keycloak_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO keycloak_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO keycloak_user;
