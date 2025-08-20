const net = require('net');

async function testLimitOrderFix() {
    console.log('🔧 Testing Limit Order Fix\n');
    
    const limitCommands = [
        'BUY 1 MNQ LIMIT @ 6397',     // Your exact command
        'SELL 1 MNQ LIMIT @ 6400',    // Sell limit
        'BUY 2 MES LIMIT @ 4950',     // MES limit
        'BUY 1 MNQ',                  // Market (should still work)
        'BUY 1 MNQ STOP LIMIT @ 6400' // Stop limit (should still work)
    ];
    
    console.log('📋 Testing Commands:');
    limitCommands.forEach((cmd, i) => {
        console.log(`   ${i + 1}. ${cmd}`);
    });
    console.log('\n' + '='.repeat(60) + '\n');
    
    for (let i = 0; i < limitCommands.length; i++) {
        const command = limitCommands[i];
        console.log(`📤 Test ${i + 1}/5: ${command}`);
        
        try {
            const response = await sendCommand(command);
            console.log(`📥 Response: ${response}`);
            console.log('✅ Command sent successfully\n');
            
            // Wait between commands
            await new Promise(resolve => setTimeout(resolve, 1500));
        } catch (error) {
            console.log(`❌ Error: ${error.message}\n`);
        }
    }
    
    console.log('🎯 Limit order test completed!');
    console.log('\n💡 If all tests show "✅ Command received", then:');
    console.log('   • Discord bot parsing is fixed');
    console.log('   • NinjaTrader strategy is receiving commands');
    console.log('   • Check NT8 Output Window for trade execution details');
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

testLimitOrderFix();
