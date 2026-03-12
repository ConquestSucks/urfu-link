DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'user') THEN
        CREATE ROLE "user" LOGIN PASSWORD 'user';
    ELSE
        ALTER ROLE "user" WITH LOGIN PASSWORD 'user';
    END IF;
END $$;

SELECT 'CREATE DATABASE user_db OWNER "user"'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'user_db')
\gexec

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'media') THEN
        CREATE ROLE "media" LOGIN PASSWORD 'media';
    ELSE
        ALTER ROLE "media" WITH LOGIN PASSWORD 'media';
    END IF;
END $$;

SELECT 'CREATE DATABASE media_db OWNER "media"'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'media_db')
\gexec

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'keycloak') THEN
        CREATE ROLE keycloak LOGIN PASSWORD 'keycloak';
    ELSE
        ALTER ROLE keycloak WITH LOGIN PASSWORD 'keycloak';
    END IF;
END $$;

SELECT 'CREATE DATABASE keycloak OWNER keycloak'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'keycloak')
\gexec
