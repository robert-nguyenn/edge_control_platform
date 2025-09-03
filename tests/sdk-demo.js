const { init } = require('../sdks/node/dist/index');

async function sdkDemo() {
    console.log('🚀 Edge Control Node SDK Demo');
    console.log('============================');
    
    const client = init({
        baseUrl: process.env.API_BASE_URL || 'http://localhost:5000',
        apiKey: process.env.API_KEY || 'demo-key',
        pollIntervalMs: 3000, // Poll every 3 seconds for demo
        defaultDecisions: {
            'pricing.v2': false,
            'exp.freeShipping': false,
            'ui.newDashboard': false
        },
        onUpdate: (flags) => {
            console.log('📊 Flags updated at', new Date().toLocaleTimeString());
            console.log('   Available flags:', Object.keys(flags).join(', '));
        },
        onError: (error) => {
            console.error('❌ SDK Error:', error.message);
        }
    });

    // Demo users
    const users = [
        { id: 'user_001', name: 'Alice' },
        { id: 'user_042', name: 'Bob' },
        { id: 'user_123', name: 'Charlie' },
        { id: 'user_456', name: 'Diana' },
        { id: 'user_789', name: 'Eve' }
    ];

    let iteration = 0;

    // Demo loop - check flags for different users
    const demoInterval = setInterval(async () => {
        iteration++;
        console.log(`\n🎯 Iteration ${iteration} - ${new Date().toLocaleTimeString()}`);
        console.log('─'.repeat(50));

        for (const user of users) {
            try {
                const context = { userId: user.id };
                
                const pricing = await client.isEnabled('pricing.v2', context);
                const freeShipping = await client.isEnabled('exp.freeShipping', context);
                const newDashboard = await client.isEnabled('ui.newDashboard', context);
                
                const flags = [];
                if (pricing) flags.push('💰 pricing.v2');
                if (freeShipping) flags.push('🚚 freeShipping');
                if (newDashboard) flags.push('🎨 newDashboard');
                
                const flagsText = flags.length > 0 ? flags.join(', ') : '🚫 none';
                console.log(`👤 ${user.name.padEnd(8)} (${user.id}): ${flagsText}`);
                
            } catch (error) {
                console.error(`❌ Error checking flags for ${user.name}:`, error.message);
            }
        }

        // Stop after 20 iterations (about 1 minute)
        if (iteration >= 20) {
            console.log('\n🏁 Demo completed!');
            console.log('💡 Try changing flag rollout percentages in the admin UI and watch the changes here.');
            client.stop();
            clearInterval(demoInterval);
            process.exit(0);
        }
    }, 3000);

    // Graceful shutdown
    process.on('SIGINT', () => {
        console.log('\n👋 Shutting down SDK demo...');
        client.stop();
        clearInterval(demoInterval);
        process.exit(0);
    });

    console.log('⏳ Starting demo... (Press Ctrl+C to stop)');
    console.log('💡 Open http://localhost:3000 to change flag rollout percentages');
    console.log('');
}

// Start the demo
sdkDemo().catch(error => {
    console.error('❌ Demo failed:', error);
    process.exit(1);
});
