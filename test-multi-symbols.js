const net = require('net');

// Test multiple symbols to ensure the system works for all instruments
console.log('🧪 Testing Multi-Symbol Trading Commands...\n');
console.log('=' .repeat(60));

const testCommands = [
    'BUY 1 NQ',      // Nasdaq futures
    'BUY 2 ES',      // S&P 500 futures  
    'BUY 1 MNQ',     // Micro Nasdaq
    'BUY 1 MES',     // Micro S&P 500
    'SELL 1 YM',     // Dow futures
    'BUY 1 RTY',     // Russell 2000
    'CLOSE NQ',      // Close specific symbol
    'CLOSE POSITION' // Close all positions
];

async function sendCommand(command) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        
        console.log(`\n📤 Testing: "${command}"`);
        console.log('-'.repeat(40));
        
        client.connect(36971, 'localhost', () => {
            console.log('🔗 Connected to NT8');
            
            // Send the command
            client.write(command);
            console.log(`📨 Command sent: ${command}`);
            
            // Wait for response
            const timeout = setTimeout(() => {
                console.log('⏰ No response within 5 seconds');
                client.destroy();
                resolve('timeout');
            }, 5000);

            client.on('data', (data) => {
                clearTimeout(timeout);
                const response = data.toString().trim();
                console.log(`✅ NT8 Response: ${response}`);
                client.destroy();
                resolve(response);
            });
        });

        client.on('error', (err) => {
            console.log(`❌ Connection error: ${err.message}`);
            reject(err);
        });
    });
}

async function testAllSymbols() {
    console.log('🎯 Starting Multi-Symbol Test Suite');
    console.log('📋 Commands to test:', testCommands.length);
    
    let successCount = 0;
    let failCount = 0;
    
    for (let i = 0; i < testCommands.length; i++) {
        try {
            const command = testCommands[i];
            const response = await sendCommand(command);
            
            if (response === 'timeout') {
                console.log(`❌ FAILED: ${command} (timeout)`);
                failCount++;
            } else if (response.includes('✅')) {
                console.log(`✅ SUCCESS: ${command}`);
                successCount++;
            } else {
                console.log(`⚠️ UNEXPECTED: ${command} - ${response}`);
                failCount++;
            }
            
            // Wait between commands to avoid overwhelming NT8
            await new Promise(resolve => setTimeout(resolve, 1000));
            
        } catch (error) {
            console.log(`❌ ERROR: ${testCommands[i]} - ${error.message}`);
            failCount++;
        }
    }
    
    console.log('\n' + '='.repeat(60));
    console.log('🏁 Multi-Symbol Test Results:');
    console.log(`✅ Successful: ${successCount}/${testCommands.length}`);
    console.log(`❌ Failed: ${failCount}/${testCommands.length}`);
    
    if (successCount === testCommands.length) {
        console.log('🎉 ALL TESTS PASSED! Multi-symbol trading is working correctly.');
    } else {
        console.log('⚠️ Some tests failed. Check NT8 Output window for details.');
        console.log('💡 Failed commands may be due to:');
        console.log('   • Symbol not configured in NT8 strategy');
        console.log('   • Symbol not available in your data feed');
        console.log('   • Account restrictions');
    }
    
    console.log('\n📋 Next Steps:');
    console.log('1. Check NT8 Output window for detailed logs');
    console.log('2. Verify which symbols are configured in TradableInstruments');
    console.log('3. Test with your Discord bot using real commands');
    console.log('4. Check account permissions for each symbol');
}

testAllSymbols().catch(error => {
    console.error('💥 Test suite error:', error);
    process.exit(1);
});
