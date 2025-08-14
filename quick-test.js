const net = require('net');

console.log('🔧 Quick Connection Test for NT8...\n');

function quickTest() {
    const client = new net.Socket();
    
    client.setTimeout(3000);
    
    client.connect(36971, 'localhost', () => {
        console.log('✅ NT8 is listening on port 36971');
        console.log('📤 Sending test command: BUY 1 NQ');
        client.write('BUY 1 NQ');
    });
    
    client.on('data', (data) => {
        console.log('📨 Response:', data.toString());
        client.destroy();
        console.log('\n🎉 Connection test successful!');
        console.log('💡 Ready to test multi-symbol trading');
    });
    
    client.on('error', (err) => {
        console.log('❌ Connection failed:', err.code || 'Unknown error');
        console.log('\n🔧 Please follow these steps:');
        console.log('1. Open NinjaTrader 8');
        console.log('2. Go to Control Center > New > Strategy...');
        console.log('3. Find "DiscordTradeCopier" in the list');
        console.log('4. Update the "Tradable Instruments" parameter to: NQ,ES,MNQ,MES,YM,RTY');
        console.log('5. Click "Start Strategy"');
        console.log('6. Look for "✅ TCP listener started successfully" in the Output window');
        console.log('7. Run this test again');
    });
    
    client.on('timeout', () => {
        console.log('⏰ Connection timeout');
        client.destroy();
        console.log('\n🔧 NT8 strategy may not be running. Please restart it.');
    });
}

quickTest();
