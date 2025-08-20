const net = require('net');

async function testMESVariations() {
    console.log('🔍 Testing MES Symbol Variations\n');
    
    // Common variations of Micro E-mini S&P 500
    const mesVariations = [
        'MES',           // Most common
        'MES 12-24',     // With expiration
        'MES 03-25',     // March 2025
        'MES 06-25',     // June 2025
        'MES 09-25',     // September 2025
        'MES DEC24',     // December 2024
        'MES MAR25',     // March 2025
        'MESZ4',         // December 2024 format
        'MESH5',         // March 2025 format
        'ES',            // Full size (to compare)
        'NQ',            // Nasdaq (to verify working symbols)
    ];
    
    for (let symbol of mesVariations) {
        console.log(`\n📤 Testing: BUY 1 ${symbol}`);
        
        try {
            const response = await sendCommand(`BUY 1 ${symbol}`);
            console.log(`📥 Response: ${response}`);
            console.log('✅ Command accepted');
        } catch (error) {
            console.log(`❌ Error: ${error.message}`);
        }
        
        // Short delay between tests
        await new Promise(resolve => setTimeout(resolve, 1000));
    }
    
    console.log('\n🎯 Symbol variation testing completed');
    console.log('\n💡 Check NinjaTrader Output Window to see which symbols are found');
}

function sendCommand(command) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        let responseData = '';
        
        const timeout = setTimeout(() => {
            client.destroy();
            reject(new Error('Connection timeout'));
        }, 3000);
        
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

testMESVariations();
