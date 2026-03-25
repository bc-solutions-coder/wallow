-- init-db.sql
-- Creates separate schemas for each module (data isolation)

-- Identity module schema
CREATE SCHEMA IF NOT EXISTS identity;

-- Billing module schema
CREATE SCHEMA IF NOT EXISTS billing;

-- Storage module schema
CREATE SCHEMA IF NOT EXISTS storage;

-- Notifications module schema
CREATE SCHEMA IF NOT EXISTS notifications;

-- Messaging module schema
CREATE SCHEMA IF NOT EXISTS messaging;

-- Announcements module schema
CREATE SCHEMA IF NOT EXISTS announcements;

-- Inquiries module schema
CREATE SCHEMA IF NOT EXISTS inquiries;

-- Audit schema
CREATE SCHEMA IF NOT EXISTS audit;

-- Grant permissions to the application user
DO $$
DECLARE
    schema_name TEXT;
BEGIN
    FOR schema_name IN SELECT unnest(ARRAY['identity', 'billing', 'storage', 'notifications', 'messaging', 'announcements', 'inquiries', 'audit'])
    LOOP
        EXECUTE format('GRANT ALL PRIVILEGES ON SCHEMA %I TO %I', schema_name, current_user);
        EXECUTE format('GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA %I TO %I', schema_name, current_user);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT ALL PRIVILEGES ON TABLES TO %I', schema_name, current_user);
    END LOOP;
END $$;
