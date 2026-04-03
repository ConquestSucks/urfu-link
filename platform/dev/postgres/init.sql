-- Databases for individual services
-- Each service owns its own database; only postgres superuser creates them at startup.

CREATE DATABASE user_db;
CREATE USER "user" WITH PASSWORD 'user';
GRANT ALL PRIVILEGES ON DATABASE user_db TO "user";
\c user_db
GRANT ALL ON SCHEMA public TO "user";

\c postgres
CREATE DATABASE media_db;
CREATE USER media WITH PASSWORD 'media';
GRANT ALL PRIVILEGES ON DATABASE media_db TO media;
\c media_db
GRANT ALL ON SCHEMA public TO media;
