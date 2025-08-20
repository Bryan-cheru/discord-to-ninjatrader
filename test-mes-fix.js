const net = require('net');

async function testMESAfterFix() {
    console.log('🔧 Testing MES Commands After Null Reference Fix\n');
    
    try {
        console.log('📤 Sending: BUY 1 MES');
        const response = await sendCommand('BUY 1 MES');
        console.log('📥 Response:', response);
        console.log('✅ MES command successful after fix!');
        
        console.log('\n💡 Next steps:');
        console.log('1. Check NinjaTrader Output Window for error messages');
        console.log('2. Make sure MES chart is loaded or MES is in TradableInstruments');
        console.log('3. Verify your account has access to MES');
        
    } catch (error) {
        console.log('❌ Error:', error.message);
        console.log('\n🔍 If connection failed:');
        console.log('1. Make sure NinjaTrader strategy is running');
        console.log('2. Check that the strategy started without null reference errors');
        console.log('3. Look in Output Window for "TCP listener started successfully"');
    }
}

function sendCommand(command) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        let responseData = '';
        
        const timeout = setTimeout(() => {
            client.destroy();
            reject(new Error('Connection timeout - Strategy may not be running'));
        }, 5000);
        
        client.connect(36971, 'localhost', () => {
            client.write(command);
        });
        
        client.on('data', (data) => {
            responseData += data.toString();
            clearTimeout(timeout);
            client.destroy();
            resolve(responseData.trim());
        });
        
        client.on('error', (error) => {
            clearTimeout(timeout);
            reject(error);
        });
        
        client.on('close', () => {
            clearTimeout(timeout);
            if (!responseData) {
                reject(new Error('No response received'));
            }
        });
    });
}

testMESAfterFix();
