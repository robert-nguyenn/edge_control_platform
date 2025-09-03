const { init } = require('./dist/index');

async function demo() {
  console.log('🚀 Starting Edge Control Node SDK Demo');
  
  const client = init({
    baseUrl: 'http://localhost:5000',
    apiKey: 'demo-key',
    pollIntervalMs: 2000,
    defaultDecisions: {
      'pricing.v2': false,
      'exp.freeShipping': false
    },
    onUpdate: (flags) => {
      console.log('📊 Flags updated:', Object.keys(flags));
    },
    onError: (error) => {
      console.error('❌ SDK Error:', error.message);
    }
  });

  // Demo loop
  setInterval(async () => {
    try {
      const user1 = await client.isEnabled('pricing.v2', { userId: 'u123' });
      const user2 = await client.isEnabled('pricing.v2', { userId: 'u456' });
      const freeShipping = await client.isEnabled('exp.freeShipping', { userId: 'u123' });
      
      console.log(`🎯 pricing.v2 - u123: ${user1}, u456: ${user2}`);
      console.log(`🚚 freeShipping - u123: ${freeShipping}`);
      console.log('---');
    } catch (error) {
      console.error('Error checking flags:', error.message);
    }
  }, 2000);
}

demo().catch(console.error);
