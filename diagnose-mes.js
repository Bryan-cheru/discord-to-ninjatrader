const net = require('net');

async function diagnoseMESIssue() {
    console.log('🔍 Diagnosing MES Issue\n');
    
    const testCommands = [
        'BUY 1 MES',
        'BUY 1 MES LIMIT @ 4950', 
        'BUY 1 MES STOP LIMIT @ 4950',
        'SELL 1 MES',
        'CLOSE MES'
    ];
    
    for (let i = 0; i < testCommands.length; i++) {
        const command = testCommands[i];
        console.log(`\n📤 Test ${i + 1}/5: ${command}`);
        
        try {
            const response = await sendCommand(command);
            console.log(`📥 Response: ${response}`);
            console.log('✅ Command sent successfully');
            
            // Wait between commands to avoid flooding
            await new Promise(resolve => setTimeout(resolve, 2000));
            
        } catch (error) {
            console.log(`❌ Error: ${error.message}`);
        }
    }
    
    console.log('\n🎯 All MES tests completed');
    console.log('\n📋 Check NinjaTrader Output Window for:');
    console.log('   • "Looking for symbol: MES"');
    console.log('   • "Found MES at existing index X"');
    console.log('   • "Instrument MES not found" (if there\'s an issue)');
    console.log('   • Order execution confirmations');
    console.log('   • Any error messages');
}

function sendCommand(command) {
    return new Promise((resolve, reject) => {
        const client = new net.Socket();
        let responseData = '';
        
        const timeout = setTimeout(() => {
            client.destroy();
            reject(new Error('Connection timeout'));
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

diagnoseMESIssue();
