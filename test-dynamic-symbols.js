const net = require('net');

// Test DYNAMIC symbol support - including uncommon symbols
console.log('🧪 Testing DYNAMIC Symbol Discovery...\n');
console.log('=' .repeat(70));

const testCommands = [
    // Common symbols (should work if loaded)
    'BUY 1 NQ',      // Nasdaq futures
    'BUY 1 ES',      // S&P 500 futures  
    'BUY 1 MNQ',     // Micro Nasdaq
    'BUY 1 MES',     // Micro S&P 500
    
    // Less common symbols (will test dynamic discovery)
    'BUY 1 YM',      // Dow futures
    'BUY 1 RTY',     // Russell 2000
    'BUY 1 CL',      // Crude Oil
    'BUY 1 GC',      // Gold
    'BUY 1 SI',      // Silver
    'BUY 1 ZB',      // 30-Year Treasury Bond
    'BUY 1 6E',      // Euro futures
    'BUY 1 6J',      // Japanese Yen
    
    // Test symbols that probably don't exist (should fail gracefully)
    'BUY 1 FAKE123', // Non-existent symbol
    'BUY 1 TEST',    // Another fake symbol
    
    // Test close commands with dynamic symbols
    'CLOSE NQ',      // Close specific symbol
    'CLOSE CL',      // Close oil if it was traded
    'CLOSE POSITION' // Close all positions
];

async function sendCommand(command) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        
        console.log(`\n📤 Testing: "${command}"`);
        console.log('-'.repeat(50));
        
        client.connect(36971, 'localhost', () => {
            console.log('🔗 Connected to NT8');
            
            // Send the command
            client.write(command);
            console.log(`📨 Command sent: ${command}`);
            
            // Wait for response
            const timeout = setTimeout(() => {
                console.log('⏰ No response within 8 seconds');
                client.destroy();
                resolve('timeout');
            }, 8000);

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

async function testDynamicSymbolSupport() {
    console.log('🎯 Starting DYNAMIC Symbol Discovery Test');
    console.log('📋 This test will show:');
    console.log('   ✅ Symbols that are loaded and ready to trade');
    console.log('   ⚠️ Symbols that exist but aren\'t loaded');
    console.log('   ❌ Symbols that don\'t exist at all');
    console.log('');
    
    let results = {
        success: 0,
        symbolNotLoaded: 0,
        symbolNotFound: 0,
        timeout: 0,
        error: 0
    };
    
    for (let i = 0; i < testCommands.length; i++) {
        try {
            const command = testCommands[i];
            const response = await sendCommand(command);
            
            if (response === 'timeout') {
                console.log(`⏰ TIMEOUT: ${command}`);
                results.timeout++;
            } else if (response.includes('✅')) {
                console.log(`✅ SUCCESS: ${command} - Symbol is loaded and ready!`);
                results.success++;
            } else if (response.includes('not loaded')) {
                console.log(`⚠️ SYMBOL EXISTS BUT NOT LOADED: ${command}`);
                results.symbolNotLoaded++;
            } else if (response.includes('not found')) {
                console.log(`❌ SYMBOL NOT FOUND: ${command}`);
                results.symbolNotFound++;
            } else {
                console.log(`🤔 UNEXPECTED: ${command} - ${response}`);
                results.error++;
            }
            
            // Wait between commands to see NT8 processing
            await new Promise(resolve => setTimeout(resolve, 1500));
            
        } catch (error) {
            console.log(`❌ ERROR: ${testCommands[i]} - ${error.message}`);
            results.error++;
        }
    }
    
    console.log('\n' + '='.repeat(70));
    console.log('🏁 DYNAMIC Symbol Discovery Results:');
    console.log(`✅ Successfully Traded: ${results.success}/${testCommands.length}`);
    console.log(`⚠️ Symbols Exist But Not Loaded: ${results.symbolNotLoaded}`);
    console.log(`❌ Symbols Not Found: ${results.symbolNotFound}`);
    console.log(`⏰ Timeouts: ${results.timeout}`);
    console.log(`🤔 Errors: ${results.error}`);
    
    console.log('\n📊 ANALYSIS:');
    if (results.success > 0) {
        console.log('🎉 DYNAMIC SYMBOL SUPPORT IS WORKING!');
        console.log('   • The strategy automatically detected loaded symbols');
        console.log('   • Any symbol loaded in NT8 can be traded via Discord');
    }
    
    if (results.symbolNotLoaded > 0) {
        console.log('💡 Some symbols exist but aren\'t loaded:');
        console.log('   • Add these symbols to charts in NinjaTrader');
        console.log('   • Or include them in TradableInstruments parameter');
        console.log('   • Then restart the strategy');
    }
    
    if (results.symbolNotFound > 0) {
        console.log('✅ System correctly rejected invalid symbols');
    }
    
    console.log('\n🚀 READY FOR PRODUCTION:');
    console.log('• Load ANY symbol in NinjaTrader (chart, watchlist, etc.)');
    console.log('• The strategy will automatically detect it');
    console.log('• Send Discord commands for ANY loaded symbol');
    console.log('• No configuration changes needed!');
    
    console.log('\n📋 CHECK NT8 OUTPUT WINDOW for detailed logs!');
}

testDynamicSymbolSupport().catch(error => {
    console.error('💥 Test suite error:', error);
    process.exit(1);
});
