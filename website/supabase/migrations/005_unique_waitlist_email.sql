-- Add unique constraint to email column in waiting_list table
ALTER TABLE public.waiting_list ADD CONSTRAINT waiting_list_email_key UNIQUE (email);
