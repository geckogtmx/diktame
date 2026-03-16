-- Create waiting_list table
CREATE TABLE IF NOT EXISTS public.waiting_list (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Enable Row Level Security
ALTER TABLE public.waiting_list ENABLE ROW LEVEL SECURITY;

-- Allow anonymous inserts for the signup flow
CREATE POLICY "Allow anonymous inserts" ON public.waiting_list
    FOR INSERT 
    WITH CHECK (true);

-- Allow authenticated users to view entries (e.g. for a dashboard later)
CREATE POLICY "Allow authenticated users to view waitlist" ON public.waiting_list
    FOR SELECT
    USING (auth.role() = 'authenticated');
