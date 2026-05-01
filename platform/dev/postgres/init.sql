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

\c postgres
CREATE DATABASE presence_db;
CREATE USER presence WITH PASSWORD 'presence';
GRANT ALL PRIVILEGES ON DATABASE presence_db TO presence;
\c presence_db
GRANT ALL ON SCHEMA public TO presence;

\c postgres
CREATE DATABASE discipline_db;
CREATE USER discipline WITH PASSWORD 'discipline';
GRANT ALL PRIVILEGES ON DATABASE discipline_db TO discipline;
\c discipline_db
GRANT ALL ON SCHEMA public TO discipline;

\c postgres
CREATE DATABASE notification_db;
CREATE USER notification WITH PASSWORD 'notification';
GRANT ALL PRIVILEGES ON DATABASE notification_db TO notification;
\c notification_db
GRANT ALL ON SCHEMA public TO notification;
