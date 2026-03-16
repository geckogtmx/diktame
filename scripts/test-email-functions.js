import { createClient } from '@supabase/supabase-js';
import dotenv from 'dotenv';
import path from 'path';

// Load env from website directory
dotenv.config({ path: path.resolve(process.cwd(), 'website/.env.local') });

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL;
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

if (!supabaseUrl || !supabaseAnonKey) {
  console.error('Missing environment variables in website/.env.local');
  process.exit(1);
}

const supabase = createClient(supabaseUrl, supabaseAnonKey);

async function testWelcome() {
  console.log('Testing waitlist-welcome function...');
  const { data, error } = await supabase.functions.invoke('waitlist-welcome', {
    body: { record: { name: 'Test User', email: 'geckogt@gmail.com' } }
  });

  if (error) {
    console.error('Waitlist-welcome failed:', error);
  } else {
    console.log('Waitlist-welcome success:', data);
  }
}

async function testInvite() {
  console.log('Testing waitlist-invite function...');
  const { data, error } = await supabase.functions.invoke('waitlist-invite', {
    body: { senderName: 'Eduardo', recipientEmail: 'geckogt@gmail.com' }
  });

  if (error) {
    console.error('Waitlist-invite failed:', error);
  } else {
    console.log('Waitlist-invite success:', data);
  }
}

async function runTests() {
  await testWelcome();
  console.log('---');
  await testInvite();
}

runTests();
