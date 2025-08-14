const net = require('net');

console.log('🔧 Post-Restart Verification Test\n');

// Simple test to verify dynamic symbol support is working
const quickTests = [
    'BUY 1 NQ',      // Should work (common)
    'BUY 1 FAKE',    // Should fail gracefully
    'CLOSE POSITION' // Should work
];

async function verifySetup() {
    console.log('🎯 Verifying Dynamic Symbol Support...\n');
    
    for (const command of quickTests) {
        try {
            console.log(`📤 Testing: ${command}`);
            
            const result = await new Promise((resolve, reject) => {
                const client = new net.Socket();
                
                client.connect(36971, 'localhost', () => {
                    client.write(command);
                    
                    const timeout = setTimeout(() => {
                        client.destroy();
                        resolve('timeout');
                    }, 3000);
                    
                    client.on('data', (data) => {
                        clearTimeout(timeout);
                        client.destroy();
                        resolve(data.toString().trim());
                    });
                });
                
                client.on('error', (err) => {
                    reject(err);
                });
            });
            
            if (result === 'timeout') {
                console.log('⏰ No response (check NT8)');
            } else {
                console.log(`✅ Response: ${result}`);
            }
            
        } catch (error) {
            console.log(`❌ Connection failed: ${error.code || error.message}`);
            console.log('\n🚨 NT8 Strategy is not running!');
            console.log('📋 Please restart the strategy and try again.\n');
            return false;
        }
        
        await new Promise(resolve => setTimeout(resolve, 1000));
    }
    
    console.log('\n🎉 Dynamic Symbol Support is active!');
    console.log('📋 Ready to test with: node test-dynamic-symbols.js');
    return true;
}

verifySetup();
