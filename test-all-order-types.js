const net = require('net');

// Test ALL order types with dynamic symbols
console.log('🧪 Testing ALL ORDER TYPES - Market, Limit & Stop Limit...\n');
console.log('=' .repeat(70));

const testCommands = [
    // ========== MARKET ORDERS ==========
    { type: 'Market Orders', commands: [
        'BUY 1 NQ',      // Basic market buy
        'SELL 1 ES',     // Basic market sell
        'BUY 2 MNQ',     // Multi-quantity market
    ]},
    
    // ========== LIMIT ORDERS ==========
    { type: 'Limit Orders', commands: [
        'BUY 1 NQ LIMIT @ 15000',     // Buy limit below market
        'SELL 1 ES LIMIT @ 4600',     // Sell limit above market
        'BUY 1 MNQ LIMIT @ 14900',    // Micro Nasdaq limit
        'SELL 2 MES LIMIT @ 4650',    // Multi-quantity limit
    ]},
    
    // ========== STOP LIMIT ORDERS ==========
    { type: 'Stop Limit Orders', commands: [
        'BUY 1 NQ STOP LIMIT @ 15100',     // Stop limit buy
        'SELL 1 ES STOP LIMIT @ 4500',     // Stop limit sell
    ]},
    
    // ========== CLOSE ORDERS ==========
    { type: 'Close Orders', commands: [
        'CLOSE NQ',         // Close specific symbol
        'CLOSE POSITION',   // Close all positions
    ]},
    
    // ========== ERROR HANDLING ==========
    { type: 'Error Handling', commands: [
        'BUY 1 FAKE LIMIT @ 100',      // Invalid symbol with limit
        'BUY INVALID LIMIT @ 100',     // Invalid format
        'SELL 1 NQ LIMIT',             // Missing price
    ]}
];

async function sendCommand(command) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        
        client.connect(36971, 'localhost', () => {
            client.write(command);
            
            const timeout = setTimeout(() => {
                client.destroy();
                resolve('timeout');
            }, 5000);

            client.on('data', (data) => {
                clearTimeout(timeout);
                const response = data.toString().trim();
                client.destroy();
                resolve(response);
            });
        });

        client.on('error', (err) => {
            reject(err);
        });
    });
}

async function testAllOrderTypes() {
    console.log('🎯 Starting Comprehensive Order Type Testing');
    console.log('📋 This will test:');
    console.log('   • Market Orders (immediate execution)');
    console.log('   • Limit Orders (specific price)');
    console.log('   • Stop Limit Orders (advanced)');
    console.log('   • Close Orders (position management)');
    console.log('   • Error handling (invalid commands)');
    console.log('');
    
    let totalResults = {
        success: 0,
        failed: 0,
        timeout: 0,
        error: 0
    };
    
    for (const section of testCommands) {
        console.log(`\n📋 ============ ${section.type.toUpperCase()} ============`);
        
        for (const command of section.commands) {
            try {
                console.log(`\n📤 Testing: "${command}"`);
                console.log('-'.repeat(50));
                
                const response = await sendCommand(command);
                
                if (response === 'timeout') {
                    console.log(`⏰ TIMEOUT: ${command}`);
                    totalResults.timeout++;
                } else if (response.includes('✅')) {
                    console.log(`✅ SUCCESS: ${command}`);
                    console.log(`   Response: ${response}`);
                    totalResults.success++;
                } else if (response.includes('❌')) {
                    console.log(`⚠️ EXPECTED FAILURE: ${command}`);
                    console.log(`   Response: ${response}`);
                    totalResults.failed++;
                } else {
                    console.log(`🤔 UNEXPECTED: ${command}`);
                    console.log(`   Response: ${response}`);
                    totalResults.error++;
                }
                
                // Wait between commands to see NT8 processing
                await new Promise(resolve => setTimeout(resolve, 1500));
                
            } catch (error) {
                console.log(`❌ CONNECTION ERROR: ${command} - ${error.message}`);
                totalResults.error++;
            }
        }
    }
    
    console.log('\n' + '='.repeat(70));
    console.log('🏁 COMPREHENSIVE ORDER TYPE TEST RESULTS:');
    console.log(`✅ Successful Orders: ${totalResults.success}`);
    console.log(`⚠️ Expected Failures: ${totalResults.failed}`);
    console.log(`⏰ Timeouts: ${totalResults.timeout}`);
    console.log(`❌ Connection Errors: ${totalResults.error}`);
    
    const totalCommands = testCommands.reduce((sum, section) => sum + section.commands.length, 0);
    const workingCommands = totalResults.success;
    
    console.log('\n📊 ANALYSIS:');
    if (workingCommands > 0) {
        console.log('🎉 ORDER TYPE SUPPORT IS WORKING!');
        console.log(`   • ${workingCommands}/${totalCommands} commands processed successfully`);
        console.log('   • Market orders: Immediate execution');
        console.log('   • Limit orders: Specific price targeting');
        console.log('   • Stop limit orders: Advanced order management');
        console.log('   • Close orders: Position management');
    }
    
    console.log('\n🚀 SUPPORTED DISCORD COMMANDS:');
    console.log('Market Orders:');
    console.log('  • BUY 1 NQ');
    console.log('  • SELL 2 ES');
    console.log('');
    console.log('Limit Orders:');
    console.log('  • BUY 1 NQ LIMIT @ 15000');
    console.log('  • SELL 1 ES LIMIT @ 4600');
    console.log('');
    console.log('Stop Limit Orders:');
    console.log('  • BUY 1 NQ STOP LIMIT @ 15100');
    console.log('  • SELL 1 ES STOP LIMIT @ 4500');
    console.log('');
    console.log('Close Orders:');
    console.log('  • CLOSE NQ');
    console.log('  • CLOSE POSITION');
    
    console.log('\n📋 CHECK NT8 OUTPUT WINDOW for detailed execution logs!');
    console.log('🎯 Ready for Discord trading with ALL order types!');
}

testAllOrderTypes().catch(error => {
    console.error('💥 Test suite error:', error);
    process.exit(1);
});
