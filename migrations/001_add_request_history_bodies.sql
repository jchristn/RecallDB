-- Migration: Add request_body and response_body columns to request_history table
-- Date: 2026-04-03
-- Description: Captures truncated request and response bodies (up to 64KB each)
--              for display in the dashboard Request History detail modal.

ALTER TABLE request_history ADD COLUMN IF NOT EXISTS request_body TEXT;
ALTER TABLE request_history ADD COLUMN IF NOT EXISTS response_body TEXT;
